using System.Collections.Generic;
using System.Net.Sockets;
using System.Linq;
using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// 资源查找工具 - 按条件查找项目资源
/// </summary>
public class AssetFindTool : IMCPTool
{
    public string ToolName => "asset_find";
    
    public string Description => "按条件查找项目资源（路径、类型、名称等）";
    
    public MCPResponse Execute(Dictionary<string, object> parameters, TcpClient client)
    {
        try
        {
            // 获取搜索参数
            string searchPath = parameters.ContainsKey("path") ? parameters["path"].ToString() : "Assets";
            string assetType = parameters.ContainsKey("type") ? parameters["type"].ToString() : "";
            string assetName = parameters.ContainsKey("name") ? parameters["name"].ToString() : "";
            string extension = parameters.ContainsKey("extension") ? parameters["extension"].ToString() : "";
            bool recursive = parameters.ContainsKey("recursive") ? System.Convert.ToBoolean(parameters["recursive"]) : true;
            int maxResults = parameters.ContainsKey("maxResults") ? System.Convert.ToInt32(parameters["maxResults"]) : 100;
            
            // 验证搜索路径
            if (!searchPath.StartsWith("Assets") && !searchPath.StartsWith("Packages"))
            {
                searchPath = "Assets/" + searchPath.TrimStart('/');
            }
            
            // 构建搜索过滤器
            string searchFilter = "";
            if (!string.IsNullOrEmpty(assetType))
            {
                searchFilter = $"t:{assetType}";
            }
            
            // 执行搜索
            string[] guids;
            
            if (recursive)
            {
                // 递归搜索指定路径
                guids = AssetDatabase.FindAssets(searchFilter, new[] { searchPath });
            }
            else
            {
                // 仅在指定路径搜索
                guids = AssetDatabase.FindAssets(searchFilter, new[] { searchPath });
            }
            
            var results = new List<Dictionary<string, object>>();
            
            foreach (string guid in guids)
            {
                if (results.Count >= maxResults)
                {
                    break;
                }
                
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                
                // 如果不是递归搜索，过滤掉子目录的资源
                if (!recursive)
                {
                    string assetDir = Path.GetDirectoryName(assetPath);
                    if (assetDir != searchPath)
                    {
                        continue;
                    }
                }
                
                // 名称过滤
                if (!string.IsNullOrEmpty(assetName))
                {
                    string fileName = Path.GetFileNameWithoutExtension(assetPath);
                    if (!IsNameMatch(fileName, assetName))
                    {
                        continue;
                    }
                }
                
                // 扩展名过滤
                if (!string.IsNullOrEmpty(extension))
                {
                    string fileExt = Path.GetExtension(assetPath).TrimStart('.');
                    if (!string.Equals(fileExt, extension, System.StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                }
                
                // 获取资源信息
                var assetInfo = GetAssetInfo(assetPath, guid);
                if (assetInfo != null)
                {
                    results.Add(assetInfo);
                }
            }
            
            // 按路径排序
            results = results.OrderBy(r => r["path"].ToString()).ToList();
            
            var result = new Dictionary<string, object>
            {
                ["searchPath"] = searchPath,
                ["searchFilter"] = searchFilter,
                ["assetType"] = assetType,
                ["assetName"] = assetName,
                ["extension"] = extension,
                ["recursive"] = recursive,
                ["maxResults"] = maxResults,
                ["totalFound"] = results.Count,
                ["assets"] = results
            };
            
            Debug.Log($"资源搜索完成，共找到 {results.Count} 个资源");
            
            return MCPResponse.Success(result);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"资源搜索时出错: {e.Message}");
            return MCPResponse.Error($"资源搜索失败: {e.Message}");
        }
    }
    
    /// <summary>
    /// 检查名称是否匹配（支持通配符）
    /// </summary>
    private bool IsNameMatch(string fileName, string pattern)
    {
        if (string.IsNullOrEmpty(pattern))
        {
            return true;
        }
        
        // 简单的通配符支持
        if (pattern.Contains("*"))
        {
            // 将通配符转换为正则表达式
            string regexPattern = pattern.Replace("*", ".*");
            return System.Text.RegularExpressions.Regex.IsMatch(fileName, regexPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        
        // 部分匹配
        return fileName.IndexOf(pattern, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }
    
    /// <summary>
    /// 获取资源详细信息
    /// </summary>
    private Dictionary<string, object> GetAssetInfo(string assetPath, string guid)
    {
        try
        {
            var assetInfo = new Dictionary<string, object>
            {
                ["guid"] = guid,
                ["path"] = assetPath,
                ["name"] = Path.GetFileNameWithoutExtension(assetPath),
                ["extension"] = Path.GetExtension(assetPath).TrimStart('.'),
                ["directory"] = Path.GetDirectoryName(assetPath)
            };
            
            // 获取资源类型
            System.Type assetType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
            if (assetType != null)
            {
                assetInfo["type"] = assetType.Name;
                assetInfo["typeFullName"] = assetType.FullName;
            }
            
            // 获取文件大小
            string fullPath = Path.Combine(Application.dataPath, assetPath.Substring(7)); // 去掉"Assets/"
            if (File.Exists(fullPath))
            {
                FileInfo fileInfo = new FileInfo(fullPath);
                assetInfo["size"] = fileInfo.Length;
                assetInfo["lastModified"] = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");
            }
            
            // 获取资源标签
            string[] labels = AssetDatabase.GetLabels(AssetDatabase.LoadMainAssetAtPath(assetPath));
            if (labels.Length > 0)
            {
                assetInfo["labels"] = labels;
            }
            
            // 获取AssetBundle名称
            string bundleName = AssetDatabase.GetImplicitAssetBundleName(assetPath);
            if (!string.IsNullOrEmpty(bundleName))
            {
                assetInfo["bundleName"] = bundleName;
            }
            
            // 特殊类型的额外信息
            if (assetType == typeof(Texture2D))
            {
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                if (texture != null)
                {
                    assetInfo["width"] = texture.width;
                    assetInfo["height"] = texture.height;
                    assetInfo["format"] = texture.format.ToString();
                }
            }
            else if (assetType == typeof(AudioClip))
            {
                AudioClip audio = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
                if (audio != null)
                {
                    assetInfo["length"] = audio.length;
                    assetInfo["frequency"] = audio.frequency;
                    assetInfo["channels"] = audio.channels;
                }
            }
            
            return assetInfo;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"获取资源信息失败 {assetPath}: {e.Message}");
            return null;
        }
    }
    
    public string ValidateParameters(Dictionary<string, object> parameters)
    {
        // 验证maxResults参数
        if (parameters.ContainsKey("maxResults"))
        {
            if (!int.TryParse(parameters["maxResults"].ToString(), out int maxResults) || maxResults <= 0)
            {
                return "maxResults必须是大于0的整数";
            }
            
            if (maxResults > 1000)
            {
                return "maxResults不能超过1000";
            }
        }
        
        return null;
    }
}