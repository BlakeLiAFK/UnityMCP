using System.Collections.Generic;
using System.Net.Sockets;
using System.Linq;
using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// 项目结构工具 - 获取项目目录结构
/// </summary>
public class ProjectStructureTool : IMCPTool
{
    public string ToolName => "project_get_structure";
    
    public string Description => "获取项目目录结构和文件统计信息";
    
    public MCPResponse Execute(Dictionary<string, object> parameters, TcpClient client)
    {
        try
        {
            // 获取参数
            string rootPath = parameters.ContainsKey("rootPath") ? parameters["rootPath"].ToString() : "Assets";
            int maxDepth = parameters.ContainsKey("maxDepth") ? System.Convert.ToInt32(parameters["maxDepth"]) : 10;
            bool includeFiles = parameters.ContainsKey("includeFiles") ? System.Convert.ToBoolean(parameters["includeFiles"]) : true;
            string[] fileTypes = parameters.ContainsKey("fileTypes") ? 
                ((parameters["fileTypes"] as IEnumerable<object>)?.Select(x => x.ToString()).ToArray()) : null;
            
            // 验证根路径
            if (!Directory.Exists(rootPath))
            {
                return MCPResponse.Error($"根路径不存在: {rootPath}");
            }
            
            var result = new Dictionary<string, object>
            {
                ["rootPath"] = rootPath,
                ["maxDepth"] = maxDepth,
                ["includeFiles"] = includeFiles,
                ["fileTypes"] = fileTypes,
                ["timestamp"] = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };
            
            // 构建目录树
            var directoryTree = BuildDirectoryTree(rootPath, 0, maxDepth, includeFiles, fileTypes);
            result["structure"] = directoryTree;
            
            // 统计信息
            var statistics = CalculateStatistics(rootPath, includeFiles, fileTypes);
            result["statistics"] = statistics;
            
            Debug.Log($"成功获取项目结构: {rootPath}");
            
            return MCPResponse.Success(result);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"获取项目结构时出错: {e.Message}");
            return MCPResponse.Error($"获取项目结构失败: {e.Message}");
        }
    }
    
    /// <summary>
    /// 构建目录树
    /// </summary>
    private Dictionary<string, object> BuildDirectoryTree(string directoryPath, int currentDepth, int maxDepth, bool includeFiles, string[] fileTypes)
    {
        var directory = new Dictionary<string, object>
        {
            ["name"] = Path.GetFileName(directoryPath),
            ["path"] = directoryPath,
            ["type"] = "directory",
            ["depth"] = currentDepth
        };
        
        try
        {
            // 获取子目录
            if (currentDepth < maxDepth)
            {
                string[] subDirectories = Directory.GetDirectories(directoryPath)
                    .Where(d => !Path.GetFileName(d).StartsWith(".")) // 排除隐藏目录
                    .OrderBy(d => Path.GetFileName(d))
                    .ToArray();
                
                if (subDirectories.Length > 0)
                {
                    var children = new List<Dictionary<string, object>>();
                    
                    foreach (string subDir in subDirectories)
                    {
                        var subDirTree = BuildDirectoryTree(subDir, currentDepth + 1, maxDepth, includeFiles, fileTypes);
                        children.Add(subDirTree);
                    }
                    
                    directory["children"] = children;
                    directory["directoryCount"] = subDirectories.Length;
                }
            }
            
            // 获取文件
            if (includeFiles)
            {
                string[] files = Directory.GetFiles(directoryPath)
                    .Where(f => !Path.GetFileName(f).StartsWith(".")) // 排除隐藏文件
                    .Where(f => fileTypes == null || fileTypes.Contains(Path.GetExtension(f).TrimStart('.')))
                    .OrderBy(f => Path.GetFileName(f))
                    .ToArray();
                
                if (files.Length > 0)
                {
                    var fileList = new List<Dictionary<string, object>>();
                    
                    foreach (string filePath in files)
                    {
                        var fileInfo = GetFileInfo(filePath);
                        if (fileInfo != null)
                        {
                            fileList.Add(fileInfo);
                        }
                    }
                    
                    if (!directory.ContainsKey("children"))
                    {
                        directory["children"] = new List<Dictionary<string, object>>();
                    }
                    
                    ((List<Dictionary<string, object>>)directory["children"]).AddRange(fileList);
                    directory["fileCount"] = fileList.Count;
                }
            }
            
            // 获取目录信息
            DirectoryInfo dirInfo = new DirectoryInfo(directoryPath);
            directory["created"] = dirInfo.CreationTime.ToString("yyyy-MM-dd HH:mm:ss");
            directory["lastModified"] = dirInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"访问目录失败 {directoryPath}: {e.Message}");
            directory["error"] = e.Message;
        }
        
        return directory;
    }
    
    /// <summary>
    /// 获取文件信息
    /// </summary>
    private Dictionary<string, object> GetFileInfo(string filePath)
    {
        try
        {
            FileInfo fileInfo = new FileInfo(filePath);
            string extension = Path.GetExtension(filePath).TrimStart('.');
            
            var file = new Dictionary<string, object>
            {
                ["name"] = Path.GetFileName(filePath),
                ["path"] = filePath,
                ["type"] = "file",
                ["extension"] = extension,
                ["size"] = fileInfo.Length,
                ["sizeFormatted"] = FormatFileSize(fileInfo.Length),
                ["created"] = fileInfo.CreationTime.ToString("yyyy-MM-dd HH:mm:ss"),
                ["lastModified"] = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")
            };
            
            // 如果是Unity资源文件，获取额外信息
            if (filePath.StartsWith("Assets/"))
            {
                string guid = AssetDatabase.AssetPathToGUID(filePath);
                if (!string.IsNullOrEmpty(guid))
                {
                    file["guid"] = guid;
                    
                    System.Type assetType = AssetDatabase.GetMainAssetTypeAtPath(filePath);
                    if (assetType != null)
                    {
                        file["assetType"] = assetType.Name;
                    }
                    
                    // 获取资源标签
                    var asset = AssetDatabase.LoadMainAssetAtPath(filePath);
                    if (asset != null)
                    {
                        string[] labels = AssetDatabase.GetLabels(asset);
                        if (labels.Length > 0)
                        {
                            file["labels"] = labels;
                        }
                    }
                }
            }
            
            return file;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"获取文件信息失败 {filePath}: {e.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// 计算统计信息
    /// </summary>
    private Dictionary<string, object> CalculateStatistics(string rootPath, bool includeFiles, string[] fileTypes)
    {
        var statistics = new Dictionary<string, object>();
        
        try
        {
            // 统计目录数量
            int directoryCount = Directory.GetDirectories(rootPath, "*", SearchOption.AllDirectories).Length + 1; // +1包含根目录
            statistics["totalDirectories"] = directoryCount;
            
            // 统计文件
            if (includeFiles)
            {
                string[] allFiles = Directory.GetFiles(rootPath, "*", SearchOption.AllDirectories)
                    .Where(f => !Path.GetFileName(f).StartsWith("."))
                    .Where(f => fileTypes == null || fileTypes.Contains(Path.GetExtension(f).TrimStart('.')))
                    .ToArray();
                
                statistics["totalFiles"] = allFiles.Length;
                
                // 文件类型统计
                var typeStats = new Dictionary<string, int>();
                var sizeStats = new Dictionary<string, long>();
                long totalSize = 0;
                
                foreach (string file in allFiles)
                {
                    string extension = Path.GetExtension(file).TrimStart('.');
                    if (string.IsNullOrEmpty(extension))
                        extension = "无扩展名";
                    
                    typeStats[extension] = typeStats.ContainsKey(extension) ? typeStats[extension] + 1 : 1;
                    
                    try
                    {
                        FileInfo fileInfo = new FileInfo(file);
                        long fileSize = fileInfo.Length;
                        sizeStats[extension] = sizeStats.ContainsKey(extension) ? sizeStats[extension] + fileSize : fileSize;
                        totalSize += fileSize;
                    }
                    catch
                    {
                        // 忽略无法访问的文件
                    }
                }
                
                statistics["fileTypeCount"] = typeStats.OrderByDescending(kv => kv.Value).ToDictionary(kv => kv.Key, kv => kv.Value);
                statistics["fileTypeSize"] = sizeStats.OrderByDescending(kv => kv.Value).ToDictionary(kv => kv.Key, kv => kv.Value);
                statistics["totalSize"] = totalSize;
                statistics["totalSizeFormatted"] = FormatFileSize(totalSize);
                
                // 最大文件
                if (allFiles.Length > 0)
                {
                    var largestFile = allFiles
                        .Select(f => new { Path = f, Size = new FileInfo(f).Length })
                        .OrderByDescending(f => f.Size)
                        .FirstOrDefault();
                    
                    if (largestFile != null)
                    {
                        statistics["largestFile"] = new Dictionary<string, object>
                        {
                            ["path"] = largestFile.Path,
                            ["size"] = largestFile.Size,
                            ["sizeFormatted"] = FormatFileSize(largestFile.Size)
                        };
                    }
                }
            }
            
            // Unity特有统计
            if (rootPath.StartsWith("Assets"))
            {
                var unityStats = GetUnitySpecificStatistics(rootPath);
                statistics["unity"] = unityStats;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"计算统计信息失败: {e.Message}");
            statistics["error"] = e.Message;
        }
        
        return statistics;
    }
    
    /// <summary>
    /// 获取Unity特有统计信息
    /// </summary>
    private Dictionary<string, object> GetUnitySpecificStatistics(string rootPath)
    {
        var unityStats = new Dictionary<string, object>();
        
        try
        {
            // 统计资源类型
            string[] guids = AssetDatabase.FindAssets("", new[] { rootPath });
            var assetTypes = new Dictionary<string, int>();
            
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                System.Type assetType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
                
                if (assetType != null)
                {
                    string typeName = assetType.Name;
                    assetTypes[typeName] = assetTypes.ContainsKey(typeName) ? assetTypes[typeName] + 1 : 1;
                }
            }
            
            unityStats["assetTypes"] = assetTypes.OrderByDescending(kv => kv.Value).ToDictionary(kv => kv.Key, kv => kv.Value);
            unityStats["totalAssets"] = guids.Length;
            
            // 场景文件统计
            string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { rootPath });
            unityStats["sceneCount"] = sceneGuids.Length;
            
            // 预制体统计
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { rootPath });
            unityStats["prefabCount"] = prefabGuids.Length;
            
            // 脚本统计
            string[] scriptGuids = AssetDatabase.FindAssets("t:Script", new[] { rootPath });
            unityStats["scriptCount"] = scriptGuids.Length;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"获取Unity统计信息失败: {e.Message}");
            unityStats["error"] = e.Message;
        }
        
        return unityStats;
    }
    
    /// <summary>
    /// 格式化文件大小
    /// </summary>
    private string FormatFileSize(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int counter = 0;
        decimal number = bytes;
        
        while (System.Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
        }
        
        return string.Format("{0:n1} {1}", number, suffixes[counter]);
    }
    
    public string ValidateParameters(Dictionary<string, object> parameters)
    {
        // 验证maxDepth参数
        if (parameters.ContainsKey("maxDepth"))
        {
            if (!int.TryParse(parameters["maxDepth"].ToString(), out int maxDepth) || maxDepth < 1 || maxDepth > 20)
            {
                return "maxDepth必须是1到20之间的整数";
            }
        }
        
        return null;
    }
}