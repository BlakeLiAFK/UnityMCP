using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEditor;

/// <summary>
/// MCP服务器管理器 - 负责启动和管理Go MCP服务器进程
/// </summary>
public class MCPServerManager
{
    public enum ServerStatus
    {
        Stopped,
        Starting,
        Running,
        Error
    }

    private Process mcpProcess;
    private ServerStatus status = ServerStatus.Stopped;
    private string mcpPort = "13000";
    private string unityPort = "12000";
    private bool debugMode = false;
    private string errorMessage = "";
    private string customExecutablePath = ""; // 自定义可执行文件路径

    // 事件委托
    public System.Action<ServerStatus> OnStatusChanged;
    public System.Action<string> OnLogMessage;

    /// <summary>
    /// 当前服务器状态
    /// </summary>
    public ServerStatus Status => status;

    /// <summary>
    /// MCP服务器端口
    /// </summary>
    public string MCPPort
    {
        get => mcpPort;
        set => mcpPort = value;
    }

    /// <summary>
    /// Unity TCP服务器端口
    /// </summary>
    public string UnityPort
    {
        get => unityPort;
        set => unityPort = value;
    }

    /// <summary>
    /// Debug模式
    /// </summary>
    public bool DebugMode
    {
        get => debugMode;
        set => debugMode = value;
    }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string ErrorMessage => errorMessage;

    /// <summary>
    /// 获取MCP服务器URL
    /// </summary>
    public string ServerURL => $"http://localhost:{mcpPort}";

    /// <summary>
    /// 自定义MCP服务器可执行文件路径
    /// </summary>
    public string ExecutablePath
    {
        get => customExecutablePath;
        set => customExecutablePath = value;
    }

    /// <summary>
    /// 启动MCP服务器
    /// </summary>
    public bool StartMCPServer()
    {
        if (status == ServerStatus.Running)
        {
            LogMessage("MCP服务器已在运行中");
            return true;
        }

        try
        {
            string executablePath = GetMCPExecutablePath();
            if (string.IsNullOrEmpty(executablePath))
            {
                SetError("找不到MCP服务器可执行文件");
                return false;
            }

            LogMessage($"启动MCP服务器: {executablePath}");

            // 构建启动参数
            string arguments = $"-port={mcpPort} -unity-host=localhost -unity-port={unityPort}";
            if (debugMode)
            {
                arguments += " -debug";
            }

            // 配置进程启动信息
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(executablePath)
            };

            // 启动进程
            mcpProcess = new Process { StartInfo = startInfo };

            // 设置输出事件处理
            mcpProcess.OutputDataReceived += OnProcessOutputReceived;
            mcpProcess.ErrorDataReceived += OnProcessErrorReceived;
            mcpProcess.Exited += OnProcessExited;
            mcpProcess.EnableRaisingEvents = true;

            if (mcpProcess.Start())
            {
                mcpProcess.BeginOutputReadLine();
                mcpProcess.BeginErrorReadLine();

                SetStatus(ServerStatus.Starting);
                LogMessage($"MCP服务器进程已启动 (PID: {mcpProcess.Id})");

                // 延迟检查启动状态
                EditorApplication.delayCall += () => CheckServerStarted();

                return true;
            }
            else
            {
                SetError("启动MCP服务器进程失败");
                return false;
            }
        }
        catch (Exception ex)
        {
            SetError($"启动MCP服务器异常: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 停止MCP服务器
    /// </summary>
    public void StopMCPServer()
    {
        if (mcpProcess != null && !mcpProcess.HasExited)
        {
            try
            {
                LogMessage("正在关闭MCP服务器...");
                mcpProcess.Kill();
                mcpProcess.WaitForExit(5000); // 等待5秒
                mcpProcess.Dispose();
                mcpProcess = null;
                SetStatus(ServerStatus.Stopped);
                LogMessage("MCP服务器已关闭");
            }
            catch (Exception ex)
            {
                LogMessage($"关闭MCP服务器时出错: {ex.Message}");
            }
        }
        else
        {
            SetStatus(ServerStatus.Stopped);
        }
    }

    /// <summary>
    /// 获取当前平台的MCP可执行文件路径
    /// </summary>
    private string GetMCPExecutablePath()
    {
        // 如果设置了自定义路径，优先使用自定义路径
        if (!string.IsNullOrEmpty(customExecutablePath) && File.Exists(customExecutablePath))
        {
            return customExecutablePath;
        }

        // 否则使用默认路径
        string baseDir = Path.Combine(Application.dataPath, "Editor", "UnityMCP", "bin");

        switch (Application.platform)
        {
            case RuntimePlatform.WindowsEditor:
                return Path.Combine(baseDir, "windows", "unity-mcp-server.exe");

            case RuntimePlatform.OSXEditor:
                return Path.Combine(baseDir, "darwin", "unity-mcp-server");

            case RuntimePlatform.LinuxEditor:
                return Path.Combine(baseDir, "linux", "unity-mcp-server");

            default:
                return null;
        }
    }

    /// <summary>
    /// 检查MCP服务器是否已启动
    /// </summary>
    private void CheckServerStarted()
    {
        if (status == ServerStatus.Starting && mcpProcess != null && !mcpProcess.HasExited)
        {
            // 简单检查：如果进程还在运行，认为启动成功
            SetStatus(ServerStatus.Running);
            LogMessage($"MCP服务器运行中 - {ServerURL}");
        }
    }

    /// <summary>
    /// 获取AI工具配置URL列表
    /// </summary>
    public Dictionary<string, string> GetAIToolConfigs()
    {
        string sseUrl = ServerURL;  // 主SSE服务器 (端口13000)
        return new Dictionary<string, string>
        {
            { "SSE端点", $"{sseUrl}/sse" },
            { "健康检查", $"{sseUrl}/health" },
            { "工具列表", $"{sseUrl}/tools" }
        };
    }

    // 事件处理
    private void OnProcessOutputReceived(object sender, DataReceivedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Data))
        {
            LogMessage($"[MCP] {e.Data}");
        }
    }

    private void OnProcessErrorReceived(object sender, DataReceivedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Data))
        {
            LogMessage($"[MCP错误] {e.Data}");
        }
    }

    private void OnProcessExited(object sender, EventArgs e)
    {
        LogMessage("MCP服务器进程已退出");
        SetStatus(ServerStatus.Stopped);
    }

    // 辅助方法
    private void SetStatus(ServerStatus newStatus)
    {
        if (status != newStatus)
        {
            status = newStatus;
            errorMessage = "";
            OnStatusChanged?.Invoke(status);
        }
    }

    private void SetError(string message)
    {
        errorMessage = message;
        status = ServerStatus.Error;
        LogMessage($"[错误] {message}");
        OnStatusChanged?.Invoke(status);
    }

    private void LogMessage(string message)
    {
        MCPLogger.Info(message, "MCP_SERVER");
        OnLogMessage?.Invoke(message);
    }

    /// <summary>
    /// 清理资源
    /// </summary>
    public void Dispose()
    {
        StopMCPServer();
    }
    
}