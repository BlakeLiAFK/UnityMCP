using System.Collections.Generic;
using System.Net.Sockets;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using System.Reflection;

/// <summary>
/// Editor日志读取工具 - 读取Unity Editor的Console日志
/// </summary>
public class EditorLogTool : IMCPTool
{
    public string ToolName => "editor_get_logs";
    
    public string Description => "读取Unity Editor Console日志（错误、警告、普通日志）";
    
    private static System.Type logEntriesType;
    private static MethodInfo getLogCountMethod;
    private static MethodInfo getLogEntryMethod;
    private static MethodInfo clearMethod;
    
    static EditorLogTool()
    {
        InitializeReflection();
    }
    
    public MCPResponse Execute(Dictionary<string, object> parameters, TcpClient client)
    {
        try
        {
            int maxLogs = parameters.ContainsKey("maxLogs") ? System.Convert.ToInt32(parameters["maxLogs"]) : 100;
            string logLevel = parameters.ContainsKey("logLevel") ? parameters["logLevel"].ToString().ToLower() : "all";
            bool clearLogs = parameters.ContainsKey("clearLogs") ? System.Convert.ToBoolean(parameters["clearLogs"]) : false;
            bool includeStackTrace = parameters.ContainsKey("includeStackTrace") ? 
                System.Convert.ToBoolean(parameters["includeStackTrace"]) : false;
            
            var result = new Dictionary<string, object>
            {
                ["maxLogs"] = maxLogs,
                ["logLevel"] = logLevel,
                ["clearLogs"] = clearLogs,
                ["includeStackTrace"] = includeStackTrace,
                ["timestamp"] = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };
            
            if (logEntriesType == null)
            {
                return MCPResponse.Error("无法访问Unity Editor日志系统，可能是Unity版本不兼容");
            }
            
            // 获取日志总数
            int totalLogCount = (int)getLogCountMethod.Invoke(null, null);
            result["totalLogCount"] = totalLogCount;
            
            if (totalLogCount == 0)
            {
                result["logs"] = new List<Dictionary<string, object>>();
                result["retrievedCount"] = 0;
                result["message"] = "没有找到日志";
                return MCPResponse.Success(result);
            }
            
            // 收集日志
            var logs = new List<Dictionary<string, object>>();
            int startIndex = System.Math.Max(0, totalLogCount - maxLogs);
            
            for (int i = startIndex; i < totalLogCount; i++)
            {
                try
                {
                    var logEntry = GetLogEntry(i, includeStackTrace);
                    if (logEntry != null && ShouldIncludeLog(logEntry, logLevel))
                    {
                        logs.Add(logEntry);
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"读取日志条目 {i} 时出错: {e.Message}");
                }
            }
            
            result["logs"] = logs;
            result["retrievedCount"] = logs.Count;
            
            // 统计信息
            var statistics = CalculateLogStatistics(logs);
            result["statistics"] = statistics;
            
            // 清除日志（如果请求）
            if (clearLogs)
            {
                try
                {
                    clearMethod?.Invoke(null, null);
                    result["logsCleared"] = true;
                    result["message"] = $"成功获取 {logs.Count} 条日志并清除了Console";
                }
                catch (System.Exception e)
                {
                    result["clearError"] = e.Message;
                    result["message"] = $"成功获取 {logs.Count} 条日志，但清除Console失败";
                }
            }
            else
            {
                result["message"] = $"成功获取 {logs.Count} 条日志";
            }
            
            Debug.Log($"Editor日志读取完成，获取了 {logs.Count} 条日志");
            
            return MCPResponse.Success(result);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"读取Editor日志时出错: {e.Message}");
            return MCPResponse.Error($"读取Editor日志失败: {e.Message}");
        }
    }
    
    /// <summary>
    /// 初始化反射访问Unity内部日志API
    /// </summary>
    private static void InitializeReflection()
    {
        try
        {
            // 获取LogEntries类型（Unity内部API）
            logEntriesType = System.Type.GetType("UnityEditor.LogEntries,UnityEditor");
            
            if (logEntriesType != null)
            {
                getLogCountMethod = logEntriesType.GetMethod("GetCount", BindingFlags.Static | BindingFlags.Public);
                clearMethod = logEntriesType.GetMethod("Clear", BindingFlags.Static | BindingFlags.Public);
                
                // 获取日志条目的方法可能因Unity版本而异
                var getMethods = logEntriesType.GetMethods(BindingFlags.Static | BindingFlags.Public)
                    .Where(m => m.Name.Contains("GetEntryInternal") || m.Name.Contains("GetFirstTwoLinesEntryTextAndModeInternal"))
                    .ToArray();
                
                if (getMethods.Length > 0)
                {
                    getLogEntryMethod = getMethods[0];
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"初始化日志反射失败: {e.Message}");
        }
    }
    
    /// <summary>
    /// 获取指定索引的日志条目
    /// </summary>
    private Dictionary<string, object> GetLogEntry(int index, bool includeStackTrace)
    {
        try
        {
            if (getLogEntryMethod == null)
            {
                return null;
            }
            
            // 尝试不同的方法签名（Unity版本兼容性）
            object[] parameters = new object[] { index, null, null };
            getLogEntryMethod.Invoke(null, parameters);
            
            string message = parameters[1]?.ToString() ?? "";
            string file = parameters[2]?.ToString() ?? "";
            
            // 解析日志级别（从消息前缀推断）
            string level = "Log";
            if (message.Contains("[Error]") || message.Contains("Error:"))
                level = "Error";
            else if (message.Contains("[Warning]") || message.Contains("Warning:"))
                level = "Warning";
            else if (message.Contains("[Exception]") || message.Contains("Exception:"))
                level = "Exception";
            
            var logEntry = new Dictionary<string, object>
            {
                ["index"] = index,
                ["message"] = message,
                ["file"] = file,
                ["level"] = level,
                ["timestamp"] = System.DateTime.Now.ToString("HH:mm:ss")
            };
            
            // 尝试获取堆栈跟踪信息
            if (includeStackTrace && !string.IsNullOrEmpty(file))
            {
                logEntry["stackTrace"] = file;
            }
            
            return logEntry;
        }
        catch (System.Exception e)
        {
            return new Dictionary<string, object>
            {
                ["index"] = index,
                ["message"] = $"读取日志条目失败: {e.Message}",
                ["level"] = "Error",
                ["timestamp"] = System.DateTime.Now.ToString("HH:mm:ss")
            };
        }
    }
    
    /// <summary>
    /// 检查是否应该包含此日志
    /// </summary>
    private bool ShouldIncludeLog(Dictionary<string, object> logEntry, string logLevel)
    {
        if (logLevel == "all")
            return true;
        
        string entryLevel = logEntry["level"].ToString().ToLower();
        
        switch (logLevel)
        {
            case "error":
                return entryLevel == "error" || entryLevel == "exception";
            case "warning":
                return entryLevel == "warning";
            case "log":
                return entryLevel == "log";
            case "exception":
                return entryLevel == "exception";
            default:
                return true;
        }
    }
    
    /// <summary>
    /// 计算日志统计信息
    /// </summary>
    private Dictionary<string, object> CalculateLogStatistics(List<Dictionary<string, object>> logs)
    {
        var stats = new Dictionary<string, object>
        {
            ["totalRetrieved"] = logs.Count
        };
        
        if (logs.Count == 0)
            return stats;
        
        // 按级别统计
        var levelCounts = new Dictionary<string, int>();
        foreach (var log in logs)
        {
            string level = log["level"].ToString();
            levelCounts[level] = levelCounts.ContainsKey(level) ? levelCounts[level] + 1 : 1;
        }
        
        stats["levelCounts"] = levelCounts;
        stats["errorCount"] = levelCounts.ContainsKey("Error") ? levelCounts["Error"] : 0;
        stats["warningCount"] = levelCounts.ContainsKey("Warning") ? levelCounts["Warning"] : 0;
        stats["logCount"] = levelCounts.ContainsKey("Log") ? levelCounts["Log"] : 0;
        stats["exceptionCount"] = levelCounts.ContainsKey("Exception") ? levelCounts["Exception"] : 0;
        
        // 最近的错误和警告
        var recentErrors = logs.Where(l => l["level"].ToString() == "Error" || l["level"].ToString() == "Exception")
            .Take(5).Select(l => l["message"].ToString()).ToList();
        var recentWarnings = logs.Where(l => l["level"].ToString() == "Warning")
            .Take(5).Select(l => l["message"].ToString()).ToList();
        
        if (recentErrors.Count > 0)
            stats["recentErrors"] = recentErrors;
        if (recentWarnings.Count > 0)
            stats["recentWarnings"] = recentWarnings;
        
        return stats;
    }
    
    public string ValidateParameters(Dictionary<string, object> parameters)
    {
        // 验证maxLogs参数
        if (parameters.ContainsKey("maxLogs"))
        {
            if (!int.TryParse(parameters["maxLogs"].ToString(), out int maxLogs) || maxLogs <= 0)
            {
                return "maxLogs必须是大于0的整数";
            }
            
            if (maxLogs > 1000)
            {
                return "maxLogs不能超过1000";
            }
        }
        
        // 验证logLevel参数
        if (parameters.ContainsKey("logLevel"))
        {
            string logLevel = parameters["logLevel"].ToString().ToLower();
            string[] validLevels = { "all", "error", "warning", "log", "exception" };
            
            bool isValid = false;
            foreach (string level in validLevels)
            {
                if (logLevel == level)
                {
                    isValid = true;
                    break;
                }
            }
            
            if (!isValid)
            {
                return $"logLevel必须是以下值之一: {string.Join(", ", validLevels)}";
            }
        }
        
        return null;
    }
}