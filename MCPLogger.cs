using System;
using System.IO;
using UnityEngine;

/// <summary>
/// MCP系统日志管理器
/// </summary>
public static class MCPLogger
{
    private static string logFilePath;
    private static bool enableFileLogging = true;
    
    static MCPLogger()
    {
        // 设置日志文件路径
        string logDir = Path.Combine(Application.dataPath, "Editor", "UnityMCP", "Logs");
        if (!Directory.Exists(logDir))
        {
            Directory.CreateDirectory(logDir);
        }
        
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        logFilePath = Path.Combine(logDir, $"mcp_log_{timestamp}.txt");
        
        // 写入日志开始标记
        WriteToFile($"=== MCP日志开始 [{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ===");
    }
    
    /// <summary>
    /// 记录信息日志
    /// </summary>
    public static void Info(string message, string category = "MCP")
    {
        string logMessage = $"[INFO][{category}] {message}";
        Debug.Log(logMessage);
        WriteToFile($"{DateTime.Now:HH:mm:ss} - {logMessage}");
    }
    
    /// <summary>
    /// 记录警告日志
    /// </summary>
    public static void Warning(string message, string category = "MCP")
    {
        string logMessage = $"[WARNING][{category}] {message}";
        Debug.LogWarning(logMessage);
        WriteToFile($"{DateTime.Now:HH:mm:ss} - {logMessage}");
    }
    
    /// <summary>
    /// 记录错误日志
    /// </summary>
    public static void Error(string message, string category = "MCP")
    {
        string logMessage = $"[ERROR][{category}] {message}";
        Debug.LogError(logMessage);
        WriteToFile($"{DateTime.Now:HH:mm:ss} - {logMessage}");
    }
    
    /// <summary>
    /// 记录异常日志
    /// </summary>
    public static void Exception(Exception exception, string message = null, string category = "MCP")
    {
        string logMessage = string.IsNullOrEmpty(message) 
            ? $"[EXCEPTION][{category}] {exception.Message}" 
            : $"[EXCEPTION][{category}] {message}: {exception.Message}";
            
        Debug.LogError(logMessage);
        WriteToFile($"{DateTime.Now:HH:mm:ss} - {logMessage}");
        WriteToFile($"Stack Trace: {exception.StackTrace}");
    }
    
    /// <summary>
    /// 记录网络相关日志
    /// </summary>
    public static void Network(string message, bool isIncoming = true)
    {
        string direction = isIncoming ? "IN" : "OUT";
        Info($"[{direction}] {message}", "NETWORK");
    }
    
    /// <summary>
    /// 记录工具执行日志
    /// </summary>
    public static void Tool(string toolName, string message, bool success = true)
    {
        string status = success ? "SUCCESS" : "FAILED";
        Info($"[{status}] {toolName}: {message}", "TOOL");
    }
    
    /// <summary>
    /// 写入文件
    /// </summary>
    private static void WriteToFile(string message)
    {
        if (!enableFileLogging) return;
        
        try
        {
            File.AppendAllText(logFilePath, message + Environment.NewLine);
        }
        catch (Exception)
        {
            // 忽略文件写入异常，避免无限循环
        }
    }
    
    /// <summary>
    /// 设置是否启用文件日志
    /// </summary>
    public static void SetFileLogging(bool enabled)
    {
        enableFileLogging = enabled;
        Info($"文件日志记录已{(enabled ? "启用" : "禁用")}");
    }
    
    /// <summary>
    /// 获取当前日志文件路径
    /// </summary>
    public static string GetLogFilePath()
    {
        return logFilePath;
    }
}
