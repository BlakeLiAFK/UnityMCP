using System.Collections.Generic;
using System.Net.Sockets;
using System.Linq;
using UnityEngine;
using UnityEditor;

/// <summary>
/// 资源依赖关系工具 - 获取资源依赖关系
/// </summary>
public class AssetDependencyTool : IMCPTool
{
    public string ToolName => "asset_get_dependencies";
    
    public string Description => "获取资源依赖关系（依赖项和被依赖项）";
    
    public MCPResponse Execute(Dictionary<string, object> parameters, TcpClient client)
    {
        try
        {
            // 获取必需参数
            if (!parameters.ContainsKey("assetPath"))
            {
                return MCPResponse.Error("缺少必需参数: assetPath");
            }
            
            string assetPath = parameters["assetPath"].ToString();
            bool recursive = parameters.ContainsKey("recursive") ? 
                System.Convert.ToBoolean(parameters["recursive"]) : false;
            bool includeImplicit = parameters.ContainsKey("includeImplicit") ? 
                System.Convert.ToBoolean(parameters["includeImplicit"]) : true;
            
            // 验证资源路径
            if (!AssetDatabase.LoadMainAssetAtPath(assetPath))
            {
                return MCPResponse.Error($"资源路径不存在: {assetPath}");
            }
            
            var result = new Dictionary<string, object>
            {
                ["assetPath"] = assetPath,
                ["recursive"] = recursive,
                ["includeImplicit"] = includeImplicit
            };
            
            // 获取依赖项（此资源依赖的其他资源）
            string[] dependencies = AssetDatabase.GetDependencies(assetPath, recursive);
            var dependencyList = new List<Dictionary<string, object>>();
            
            foreach (string depPath in dependencies)
            {
                // 排除自身
                if (depPath == assetPath)
                    continue;
                
                // 如果不包含隐式依赖，过滤掉内置资源
                if (!includeImplicit && IsBuiltInAsset(depPath))
                    continue;
                
                var depInfo = GetAssetBasicInfo(depPath);
                if (depInfo != null)
                {
                    dependencyList.Add(depInfo);
                }
            }
            
            result["dependencies"] = dependencyList;
            result["dependencyCount"] = dependencyList.Count;
            
            // 获取被依赖项（依赖此资源的其他资源）
            var dependentList = new List<Dictionary<string, object>>();
            string[] allAssets = AssetDatabase.GetAllAssetPaths();
            
            foreach (string checkPath in allAssets)
            {
                if (checkPath == assetPath)
                    continue;
                
                // 检查该资源是否依赖目标资源
                string[] checkDependencies = AssetDatabase.GetDependencies(checkPath, false);
                if (checkDependencies.Contains(assetPath))
                {
                    var depInfo = GetAssetBasicInfo(checkPath);
                    if (depInfo != null)
                    {
                        dependentList.Add(depInfo);
                    }
                }
                
                // 限制搜索数量以避免性能问题
                if (dependentList.Count >= 100)
                {
                    break;
                }
            }
            
            result["dependents"] = dependentList;
            result["dependentCount"] = dependentList.Count;
            
            // 分析依赖关系
            var analysis = AnalyzeDependencies(assetPath, dependencyList, dependentList);
            result["analysis"] = analysis;
            
            Debug.Log($"成功获取资源依赖关系: {assetPath}, 依赖项: {dependencyList.Count}, 被依赖项: {dependentList.Count}");
            
            return MCPResponse.Success(result);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"获取资源依赖关系时出错: {e.Message}");
            return MCPResponse.Error($"获取资源依赖关系失败: {e.Message}");
        }
    }
    
    /// <summary>
    /// 获取资源基本信息
    /// </summary>
    private Dictionary<string, object> GetAssetBasicInfo(string assetPath)
    {
        try
        {
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            System.Type assetType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
            
            var info = new Dictionary<string, object>
            {
                ["path"] = assetPath,
                ["guid"] = guid,
                ["name"] = System.IO.Path.GetFileNameWithoutExtension(assetPath),
                ["extension"] = System.IO.Path.GetExtension(assetPath).TrimStart('.'),
                ["directory"] = System.IO.Path.GetDirectoryName(assetPath),
                ["isBuiltIn"] = IsBuiltInAsset(assetPath)
            };
            
            if (assetType != null)
            {
                info["type"] = assetType.Name;
            }
            
            // 获取文件大小
            if (System.IO.File.Exists(assetPath))
            {
                System.IO.FileInfo fileInfo = new System.IO.FileInfo(assetPath);
                info["size"] = fileInfo.Length;
            }
            
            return info;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"获取资源基本信息失败 {assetPath}: {e.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// 检查是否为内置资源
    /// </summary>
    private bool IsBuiltInAsset(string assetPath)
    {
        return assetPath.StartsWith("Library/") || 
               assetPath.StartsWith("Resources/unity_builtin_extra") ||
               assetPath == "Resources/unity_builtin_extra";
    }
    
    /// <summary>
    /// 分析依赖关系
    /// </summary>
    private Dictionary<string, object> AnalyzeDependencies(string assetPath, 
        List<Dictionary<string, object>> dependencies, 
        List<Dictionary<string, object>> dependents)
    {
        var analysis = new Dictionary<string, object>();
        
        // 依赖项分析
        var depTypes = new Dictionary<string, int>();
        var depFolders = new Dictionary<string, int>();
        long totalDepSize = 0;
        
        foreach (var dep in dependencies)
        {
            string type = dep.ContainsKey("type") ? dep["type"].ToString() : "Unknown";
            depTypes[type] = depTypes.ContainsKey(type) ? depTypes[type] + 1 : 1;
            
            string folder = dep.ContainsKey("directory") ? dep["directory"].ToString() : "";
            if (!string.IsNullOrEmpty(folder))
            {
                depFolders[folder] = depFolders.ContainsKey(folder) ? depFolders[folder] + 1 : 1;
            }
            
            if (dep.ContainsKey("size"))
            {
                totalDepSize += System.Convert.ToInt64(dep["size"]);
            }
        }
        
        analysis["dependencyTypes"] = depTypes.OrderByDescending(kv => kv.Value)
            .ToDictionary(kv => kv.Key, kv => kv.Value);
        analysis["dependencyFolders"] = depFolders.OrderByDescending(kv => kv.Value)
            .Take(10).ToDictionary(kv => kv.Key, kv => kv.Value);
        analysis["totalDependencySize"] = totalDepSize;
        
        // 被依赖项分析
        var depedentTypes = new Dictionary<string, int>();
        var dependentFolders = new Dictionary<string, int>();
        
        foreach (var dependent in dependents)
        {
            string type = dependent.ContainsKey("type") ? dependent["type"].ToString() : "Unknown";
            depedentTypes[type] = depedentTypes.ContainsKey(type) ? depedentTypes[type] + 1 : 1;
            
            string folder = dependent.ContainsKey("directory") ? dependent["directory"].ToString() : "";
            if (!string.IsNullOrEmpty(folder))
            {
                dependentFolders[folder] = dependentFolders.ContainsKey(folder) ? dependentFolders[folder] + 1 : 1;
            }
        }
        
        analysis["dependentTypes"] = depedentTypes.OrderByDescending(kv => kv.Value)
            .ToDictionary(kv => kv.Key, kv => kv.Value);
        analysis["dependentFolders"] = dependentFolders.OrderByDescending(kv => kv.Value)
            .Take(10).ToDictionary(kv => kv.Key, kv => kv.Value);
        
        // 依赖复杂度评估
        string complexity = "Simple";
        if (dependencies.Count > 50)
        {
            complexity = "High";
        }
        else if (dependencies.Count > 20)
        {
            complexity = "Medium";
        }
        
        analysis["dependencyComplexity"] = complexity;
        
        // 使用频率评估
        string usage = "Low";
        if (dependents.Count > 20)
        {
            usage = "High";
        }
        else if (dependents.Count > 5)
        {
            usage = "Medium";
        }
        
        analysis["usageFrequency"] = usage;
        
        // 建议
        var recommendations = new List<string>();
        
        if (dependencies.Count > 30)
        {
            recommendations.Add("依赖项较多，考虑重构以减少复杂度");
        }
        
        if (dependents.Count > 50)
        {
            recommendations.Add("被多个资源依赖，修改时需要谨慎");
        }
        
        if (totalDepSize > 10 * 1024 * 1024) // 10MB
        {
            recommendations.Add("依赖项总大小较大，可能影响加载性能");
        }
        
        analysis["recommendations"] = recommendations;
        
        return analysis;
    }
    
    public string ValidateParameters(Dictionary<string, object> parameters)
    {
        // 检查必需参数
        if (!parameters.ContainsKey("assetPath"))
        {
            return "缺少必需参数: assetPath";
        }
        
        string assetPath = parameters["assetPath"].ToString();
        if (string.IsNullOrEmpty(assetPath))
        {
            return "assetPath不能为空";
        }
        
        return null;
    }
}