using System;
using System.Collections.Generic;
using System.Net.Sockets;
using UnityEngine;
using Newtonsoft.Json;

/// <summary>
/// MCP消息分发器，负责将收到的消息分派给对应的工具处理
/// </summary>
public class MCPMessageDispatcher
{
    private Dictionary<string, IMCPTool> registeredTools;
    private MCPServer server;
    
    public MCPMessageDispatcher(MCPServer server)
    {
        this.server = server;
        registeredTools = new Dictionary<string, IMCPTool>();
        
        // 注册消息处理事件
        server.onMessageReceived += HandleMessage;
        
        // 注册所有工具
        RegisterAllTools();
    }
    
    /// <summary>
    /// 注册工具
    /// </summary>
    /// <param name="tool">要注册的工具</param>
    public void RegisterTool(IMCPTool tool)
    {
        if (tool == null)
        {
            Debug.LogError("尝试注册空工具");
            return;
        }
        
        if (registeredTools.ContainsKey(tool.ToolName))
        {
            Debug.LogWarning($"工具 '{tool.ToolName}' 已存在，将被覆盖");
        }
        
        registeredTools[tool.ToolName] = tool;
        Debug.Log($"工具已注册: {tool.ToolName} - {tool.Description}");
    }
    
    /// <summary>
    /// 取消注册工具
    /// </summary>
    /// <param name="toolName">工具名称</param>
    public void UnregisterTool(string toolName)
    {
        if (registeredTools.ContainsKey(toolName))
        {
            registeredTools.Remove(toolName);
            Debug.Log($"工具已取消注册: {toolName}");
        }
    }
    
    /// <summary>
    /// 注册所有工具
    /// </summary>
    private void RegisterAllTools()
    {
        Debug.Log("开始注册MCP工具...");
        
        // 注册脚本操作工具
        RegisterTool(new ScriptReadTool());
        RegisterTool(new ScriptWriteTool());
        
        // 注册场景操作工具
        RegisterTool(new SceneGetTool());
        RegisterTool(new SceneCreateObjectTool());
        RegisterTool(new SceneObjectAddComponentTool());
        
        // 注册Transform操作工具
        RegisterTool(new SceneTransformGetTool());
        RegisterTool(new SceneTransformSetTool());
        
        Debug.Log($"MCP工具注册完成，共注册 {registeredTools.Count} 个工具");
    }

    /// <summary>
    /// 处理收到的消息
    /// </summary>
    /// <param name="messageJson">JSON格式的消息</param>
    /// <param name="client">发送消息的客户端</param>
    private void HandleMessage(string messageJson, TcpClient client)
    {
        UnityMCPMainThread.AddToMainThread(() => _HandleMessage(messageJson, client));
    }
    private void _HandleMessage(string messageJson, TcpClient client)
    {
        try
        {
            // 解析JSON消息
            MCPMessage message = JsonConvert.DeserializeObject<MCPMessage>(messageJson);
            
            if (message == null)
            {
                SendErrorResponse("无效的消息格式", null, client);
                return;
            }
            
            if (string.IsNullOrEmpty(message.action))
            {
                SendErrorResponse("消息缺少action字段", message.id, client);
                return;
            }
            
            Debug.Log($"处理MCP消息: action={message.action}, id={message.id}");
            
            // 查找对应的工具
            if (!registeredTools.ContainsKey(message.action))
            {
                SendErrorResponse($"未找到工具: {message.action}", message.id, client);
                return;
            }
            
            IMCPTool tool = registeredTools[message.action];
            
            // 验证参数
            string validationError = tool.ValidateParameters(message.parameters ?? new Dictionary<string, object>());
            if (!string.IsNullOrEmpty(validationError))
            {
                SendErrorResponse($"参数验证失败: {validationError}", message.id, client);
                return;
            }
            
            // 执行工具
            MCPResponse response = tool.Execute(message.parameters ?? new Dictionary<string, object>(), client);
            response.id = message.id; // 确保响应ID与请求ID一致
            
            // 发送响应
            SendResponse(response, client);
        }
        catch (JsonException e)
        {
            Debug.LogError($"JSON解析错误: {e.Message}");
            SendErrorResponse($"JSON解析错误: {e.Message}", null, client);
        }
        catch (Exception e)
        {
            Debug.LogError($"处理消息时发生未知错误: {e.Message}");
            SendErrorResponse($"服务器内部错误: {e.Message}", null, client);
        }
    }
    
    /// <summary>
    /// 发送响应给客户端
    /// </summary>
    /// <param name="response">响应对象</param>
    /// <param name="client">目标客户端</param>
    private void SendResponse(MCPResponse response, TcpClient client)
    {
        try
        {
            string responseJson = JsonConvert.SerializeObject(response, Formatting.None);
            server.SendMessage(responseJson, client);
        }
        catch (Exception e)
        {
            Debug.LogError($"发送响应时出错: {e.Message}");
        }
    }
    
    /// <summary>
    /// 发送错误响应
    /// </summary>
    /// <param name="error">错误信息</param>
    /// <param name="id">请求ID</param>
    /// <param name="client">目标客户端</param>
    private void SendErrorResponse(string error, string id, TcpClient client)
    {
        MCPResponse response = MCPResponse.Error(error, id);
        SendResponse(response, client);
    }
    
    /// <summary>
    /// 获取所有已注册工具的信息
    /// </summary>
    /// <returns>工具信息列表</returns>
    public Dictionary<string, string> GetRegisteredTools()
    {
        Dictionary<string, string> toolsInfo = new Dictionary<string, string>();
        foreach (var tool in registeredTools)
        {
            toolsInfo[tool.Key] = tool.Value.Description;
        }
        return toolsInfo;
    }
}
