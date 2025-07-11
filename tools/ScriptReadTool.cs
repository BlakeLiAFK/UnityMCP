using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using UnityEngine;

/// <summary>
/// 脚本读取工具 - 读取指定脚本文件的内容
/// </summary>
public class ScriptReadTool : IMCPTool
{
    public string ToolName => "script_read";
    
    public string Description => "读取指定路径的脚本文件内容";
    
    public MCPResponse Execute(Dictionary<string, object> parameters, TcpClient client)
    {
        try
        {
            string filePath = parameters["path"].ToString();
            
            // 转换为绝对路径
            if (!Path.IsPathRooted(filePath))
            {
                filePath = Path.Combine(Application.dataPath, filePath);
            }
            
            // 检查文件是否存在
            if (!File.Exists(filePath))
            {
                return MCPResponse.Error($"文件不存在: {filePath}");
            }
            
            // 检查文件扩展名是否为脚本文件
            string extension = Path.GetExtension(filePath).ToLower();
            if (extension != ".cs" && extension != ".js" && extension != ".py" && extension != ".txt")
            {
                return MCPResponse.Error($"不支持的文件类型: {extension}");
            }
            
            // 读取文件内容
            string content = File.ReadAllText(filePath);
            
            var result = new Dictionary<string, object>
            {
                ["path"] = filePath,
                ["content"] = content,
                ["size"] = content.Length,
                ["extension"] = extension,
                ["fileName"] = Path.GetFileName(filePath)
            };
            
            Debug.Log($"成功读取脚本文件: {filePath} ({content.Length} 字符)");
            
            return MCPResponse.Success(result);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"读取脚本文件时出错: {e.Message}");
            return MCPResponse.Error($"读取文件失败: {e.Message}");
        }
    }
    
    public string ValidateParameters(Dictionary<string, object> parameters)
    {
        if (parameters == null || !parameters.ContainsKey("path"))
        {
            return "缺少必需参数: path";
        }
        
        if (parameters["path"] == null || string.IsNullOrEmpty(parameters["path"].ToString()))
        {
            return "path参数不能为空";
        }
        
        return null; // 验证通过
    }
}
