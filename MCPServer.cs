using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using Newtonsoft.Json;

/*
一个TCP服务器，用于接收MCP消息
协议格式：
[4字节header: payload长度] + [payload data: JSON消息]
*/

// MCP 服务器
public class MCPServer
{
    public enum Status
    {
        Stopped,
        Running,
    }
    
    public Status status = Status.Stopped;
    private TcpListener tcpListener;
    private Thread tcpListenerThread;
    private List<TcpClient> connectedClients;
    private int port;
    
    // 事件委托
    public delegate void OnMessageReceived(string message, TcpClient client);
    public OnMessageReceived onMessageReceived;
    
    public delegate void OnClientConnected(TcpClient client);
    public OnClientConnected onClientConnected;
    
    public delegate void OnClientDisconnected(TcpClient client);
    public OnClientDisconnected onClientDisconnected;
    
    public MCPServer()
    {
        connectedClients = new List<TcpClient>();
    }
    
    // 启动服务器
    public void StartServer(int port)
    {
        try
        {
            this.port = port;
            tcpListener = new TcpListener(IPAddress.Any, port);
            tcpListener.Start(); // 关键：启动TCP监听器
            tcpListenerThread = new Thread(new ThreadStart(ListenForClients));
            tcpListenerThread.IsBackground = true;
            tcpListenerThread.Start();
            status = Status.Running;
            MCPLogger.Info($"MCP服务器已启动，端口: {port}", "SERVER");
        }
        catch (Exception ex)
        {
            status = Status.Stopped;
            MCPLogger.Exception(ex, "启动服务器失败", "SERVER");
            throw;
        }
    }
    
    // 停止服务器
    public void StopServer()
    {
        try
        {
            status = Status.Stopped;
            
            // 关闭所有客户端连接
            lock (connectedClients)
            {
                foreach (var client in connectedClients)
                {
                    client?.Close();
                }
                connectedClients.Clear();
            }
            
            // 停止监听
            tcpListener?.Stop();
            
            // 终止监听线程
            if (tcpListenerThread != null && tcpListenerThread.IsAlive)
            {
                tcpListenerThread.Abort();
            }
            
            MCPLogger.Info("MCP服务器已停止", "SERVER");
        }
        catch (Exception e)
        {
            MCPLogger.Exception(e, "停止MCP服务器时出错", "SERVER");
        }
    }
    
    // 监听客户端连接
    private void ListenForClients()
    {
        MCPLogger.Info($"开始监听客户端连接...", "SERVER");
        
        while (status == Status.Running)
        {
            try
            {
                TcpClient client = tcpListener.AcceptTcpClient();
                Debug.Log($"客户端已连接: {client.Client.RemoteEndPoint}");
                
                lock (connectedClients)
                {
                    connectedClients.Add(client);
                }
                
                // 触发客户端连接事件
                onClientConnected?.Invoke(client);
                
                // 为每个客户端创建处理线程
                Thread clientThread = new Thread(() => HandleClientComm(client));
                clientThread.IsBackground = true;
                clientThread.Start();
            }
            catch (Exception e)
            {
                if (status == Status.Running)
                {
                    Debug.LogError($"接受客户端连接时出错: {e.Message}");
                }
            }
        }
    }
    
    // 处理客户端通信
    private void HandleClientComm(TcpClient client)
    {
        NetworkStream clientStream = client.GetStream();
        byte[] headerBuffer = new byte[4];
        
        try
        {
            while (client.Connected && status == Status.Running)
            {
                // 读取4字节header（消息长度）
                int headerBytesRead = 0;
                while (headerBytesRead < 4 && client.Connected)
                {
                    int bytesRead = clientStream.Read(headerBuffer, headerBytesRead, 4 - headerBytesRead);
                    if (bytesRead == 0) break;
                    headerBytesRead += bytesRead;
                }
                
                if (headerBytesRead < 4) break;
                
                // 解析消息长度（大端序）
                int messageLength = BitConverter.ToInt32(headerBuffer, 0);
                if (BitConverter.IsLittleEndian)
                {
                    messageLength = IPAddress.NetworkToHostOrder(messageLength);
                }
                
                if (messageLength <= 0 || messageLength > 1024 * 1024) // 限制消息大小
                {
                    Debug.LogWarning($"无效的消息长度: {messageLength}");
                    break;
                }
                
                // 读取payload数据
                byte[] messageBuffer = new byte[messageLength];
                int messageBytesRead = 0;
                while (messageBytesRead < messageLength && client.Connected)
                {
                    int bytesRead = clientStream.Read(messageBuffer, messageBytesRead, messageLength - messageBytesRead);
                    if (bytesRead == 0) break;
                    messageBytesRead += bytesRead;
                }
                
                if (messageBytesRead < messageLength) break;
                
                // 解析消息内容
                string message = Encoding.UTF8.GetString(messageBuffer);
                Debug.Log($"收到消息: {message}");
                
                // 触发消息接收事件
                onMessageReceived?.Invoke(message, client);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"处理客户端通信时出错: {e.Message}");
        }
        finally
        {
            // 清理连接
            lock (connectedClients)
            {
                connectedClients.Remove(client);
            }
            
            client?.Close();
            onClientDisconnected?.Invoke(client);
            Debug.Log("客户端连接已断开");
        }
    }
    
    // 发送消息给指定客户端
    public void SendMessage(string message, TcpClient client)
    {
        try
        {
            if (client?.Connected == true)
            {
                byte[] messageBytes = Encoding.UTF8.GetBytes(message);
                byte[] headerBytes = BitConverter.GetBytes(messageBytes.Length);
                
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(headerBytes); // 转换为大端序
                }
                
                NetworkStream stream = client.GetStream();
                stream.Write(headerBytes, 0, 4);
                stream.Write(messageBytes, 0, messageBytes.Length);
                stream.Flush();
                
                Debug.Log($"消息已发送: {message}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"发送消息失败: {e.Message}");
        }
    }
    
    // 广播消息给所有客户端
    public void BroadcastMessage(string message)
    {
        lock (connectedClients)
        {
            foreach (var client in connectedClients)
            {
                SendMessage(message, client);
            }
        }
    }
    
    // 获取连接的客户端数量
    public int GetConnectedClientCount()
    {
        lock (connectedClients)
        {
            return connectedClients.Count;
        }
    }
}
