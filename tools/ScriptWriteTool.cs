using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using UnityEngine;
using UnityEditor;

/// <summary>
/// 脚本写入工具 - 创建或更新脚本文件
/// </summary>
public class ScriptWriteTool : IMCPTool
{
    public string ToolName => "script_write";
    
    public string Description => "创建或更新指定路径的脚本文件";
    
    public MCPResponse Execute(Dictionary<string, object> parameters, TcpClient client)
    {
        try
        {
            string filePath = parameters["path"].ToString();
            string content = parameters["content"].ToString();
            bool overwrite = parameters.ContainsKey("overwrite") ? (bool)parameters["overwrite"] : true;
            
            // 转换为绝对路径
            if (!Path.IsPathRooted(filePath))
            {
                filePath = Path.Combine(Application.dataPath, filePath);
            }
            
            // 检查文件是否已存在
            bool fileExists = File.Exists(filePath);
            if (fileExists && !overwrite)
            {
                return MCPResponse.Error($"文件已存在且不允许覆盖: {filePath}");
            }
            
            // 确保目录存在
            string directory = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                Debug.Log($"创建目录: {directory}");
            }
            
            // 检查文件扩展名
            string extension = Path.GetExtension(filePath).ToLower();
            if (string.IsNullOrEmpty(extension))
            {
                filePath += ".cs"; // 默认为C#脚本
                extension = ".cs";
            }
            
            if (extension != ".cs" && extension != ".js" && extension != ".py" && extension != ".txt")
            {
                return MCPResponse.Error($"不支持的文件类型: {extension}");
            }
            
            // 写入文件
            File.WriteAllText(filePath, content);
            
            // 刷新Unity资源数据库
            string relativePath = filePath.Replace(Application.dataPath, "Assets");
            AssetDatabase.ImportAsset(relativePath);
            
            var result = new Dictionary<string, object>
            {
                ["path"] = filePath,
                ["relativePath"] = relativePath,
                ["size"] = content.Length,
                ["extension"] = extension,
                ["fileName"] = Path.GetFileName(filePath),
                ["created"] = !fileExists,
                ["updated"] = fileExists
            };
            
            string action = fileExists ? "更新" : "创建";
            Debug.Log($"成功{action}脚本文件: {filePath} ({content.Length} 字符)");
            
            return MCPResponse.Success(result);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"写入脚本文件时出错: {e.Message}");
            return MCPResponse.Error($"写入文件失败: {e.Message}");
        }
    }
    
    public string ValidateParameters(Dictionary<string, object> parameters)
    {
        if (parameters == null)
        {
            return "参数不能为空";
        }
        
        if (!parameters.ContainsKey("path") || parameters["path"] == null || string.IsNullOrEmpty(parameters["path"].ToString()))
        {
            return "缺少必需参数: path";
        }
        
        if (!parameters.ContainsKey("content") || parameters["content"] == null)
        {
            return "缺少必需参数: content";
        }
        
        return null; // 验证通过
    }
}
