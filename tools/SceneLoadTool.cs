using System.Collections.Generic;
using System.Net.Sockets;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// 场景加载工具 - 加载指定场景
/// </summary>
public class SceneLoadTool : IMCPTool
{
    public string ToolName => "scene_load";
    
    public string Description => "加载指定场景文件";
    
    public MCPResponse Execute(Dictionary<string, object> parameters, TcpClient client)
    {
        try
        {
            // 获取必需参数
            if (!parameters.ContainsKey("scenePath"))
            {
                return MCPResponse.Error("缺少必需参数: scenePath");
            }
            
            string scenePath = parameters["scenePath"].ToString();
            string loadMode = parameters.ContainsKey("loadMode") ? parameters["loadMode"].ToString() : "single";
            bool saveCurrentScene = parameters.ContainsKey("saveCurrentScene") ? 
                System.Convert.ToBoolean(parameters["saveCurrentScene"]) : true;
            
            // 验证场景文件是否存在
            if (!System.IO.File.Exists(scenePath))
            {
                return MCPResponse.Error($"场景文件不存在: {scenePath}");
            }
            
            var result = new Dictionary<string, object>
            {
                ["scenePath"] = scenePath,
                ["loadMode"] = loadMode,
                ["saveCurrentScene"] = saveCurrentScene,
                ["timestamp"] = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };
            
            // 获取当前场景信息
            Scene currentScene = SceneManager.GetActiveScene();
            if (currentScene.IsValid())
            {
                result["previousScene"] = new Dictionary<string, object>
                {
                    ["name"] = currentScene.name,
                    ["path"] = currentScene.path,
                    ["isDirty"] = currentScene.isDirty
                };
                
                // 检查当前场景是否需要保存
                if (saveCurrentScene && currentScene.isDirty)
                {
                    bool shouldSave = EditorUtility.DisplayDialog(
                        "保存当前场景?", 
                        $"当前场景 '{currentScene.name}' 有未保存的修改。是否要在加载新场景前保存？", 
                        "保存", "不保存");
                    
                    if (shouldSave)
                    {
                        bool saveSuccess = EditorSceneManager.SaveScene(currentScene);
                        result["currentSceneSaved"] = saveSuccess;
                        
                        if (!saveSuccess)
                        {
                            return MCPResponse.Error("保存当前场景失败，取消加载操作");
                        }
                    }
                    else
                    {
                        result["currentSceneSaved"] = false;
                        result["currentSceneDiscarded"] = true;
                    }
                }
            }
            
            // 根据加载模式加载场景
            switch (loadMode.ToLower())
            {
                case "single":
                    return LoadSceneSingle(scenePath, result);
                    
                case "additive":
                    return LoadSceneAdditive(scenePath, result);
                    
                default:
                    return MCPResponse.Error($"不支持的加载模式: {loadMode}。支持的模式: single, additive");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"加载场景时出错: {e.Message}");
            return MCPResponse.Error($"加载场景失败: {e.Message}");
        }
    }
    
    /// <summary>
    /// 单独模式加载场景（替换当前场景）
    /// </summary>
    private MCPResponse LoadSceneSingle(string scenePath, Dictionary<string, object> result)
    {
        try
        {
            Scene loadedScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            
            if (!loadedScene.IsValid())
            {
                return MCPResponse.Error($"加载场景失败: {scenePath}");
            }
            
            // 设置为活动场景
            SceneManager.SetActiveScene(loadedScene);
            
            result["success"] = true;
            result["loadedScene"] = GetSceneInfo(loadedScene);
            result["message"] = $"成功加载场景: {loadedScene.name}";
            result["totalScenesLoaded"] = SceneManager.sceneCount;
            
            Debug.Log($"成功加载场景: {scenePath} -> {loadedScene.name}");
            
            return MCPResponse.Success(result);
        }
        catch (System.Exception e)
        {
            return MCPResponse.Error($"单独模式加载场景失败: {e.Message}");
        }
    }
    
    /// <summary>
    /// 附加模式加载场景（添加到现有场景）
    /// </summary>
    private MCPResponse LoadSceneAdditive(string scenePath, Dictionary<string, object> result)
    {
        try
        {
            Scene loadedScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            
            if (!loadedScene.IsValid())
            {
                return MCPResponse.Error($"附加模式加载场景失败: {scenePath}");
            }
            
            result["success"] = true;
            result["loadedScene"] = GetSceneInfo(loadedScene);
            result["message"] = $"成功以附加模式加载场景: {loadedScene.name}";
            result["totalScenesLoaded"] = SceneManager.sceneCount;
            
            // 获取所有加载的场景信息
            var allScenes = new List<Dictionary<string, object>>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.IsValid())
                {
                    allScenes.Add(GetSceneInfo(scene));
                }
            }
            result["allLoadedScenes"] = allScenes;
            
            Debug.Log($"成功以附加模式加载场景: {scenePath} -> {loadedScene.name}");
            
            return MCPResponse.Success(result);
        }
        catch (System.Exception e)
        {
            return MCPResponse.Error($"附加模式加载场景失败: {e.Message}");
        }
    }
    
    /// <summary>
    /// 获取场景详细信息
    /// </summary>
    private Dictionary<string, object> GetSceneInfo(Scene scene)
    {
        var info = new Dictionary<string, object>
        {
            ["name"] = scene.name,
            ["path"] = scene.path,
            ["isLoaded"] = scene.isLoaded,
            ["isDirty"] = scene.isDirty,
            ["isValid"] = scene.IsValid(),
            ["buildIndex"] = scene.buildIndex,
            ["handle"] = scene.handle
        };
        
        if (scene.isLoaded)
        {
            // 统计场景中的对象
            GameObject[] rootObjects = scene.GetRootGameObjects();
            info["rootObjectCount"] = rootObjects.Length;
            
            int totalObjectCount = 0;
            foreach (GameObject rootObj in rootObjects)
            {
                totalObjectCount += CountChildrenRecursive(rootObj.transform) + 1; // +1 for the root object itself
            }
            info["totalObjectCount"] = totalObjectCount;
            
            // 获取根对象名称列表
            var rootNames = new List<string>();
            foreach (GameObject rootObj in rootObjects)
            {
                rootNames.Add(rootObj.name);
            }
            info["rootObjectNames"] = rootNames;
        }
        
        return info;
    }
    
    /// <summary>
    /// 递归计算子对象数量
    /// </summary>
    private int CountChildrenRecursive(Transform parent)
    {
        int count = parent.childCount;
        for (int i = 0; i < parent.childCount; i++)
        {
            count += CountChildrenRecursive(parent.GetChild(i));
        }
        return count;
    }
    
    public string ValidateParameters(Dictionary<string, object> parameters)
    {
        // 检查必需参数
        if (!parameters.ContainsKey("scenePath"))
        {
            return "缺少必需参数: scenePath";
        }
        
        string scenePath = parameters["scenePath"].ToString();
        if (string.IsNullOrEmpty(scenePath))
        {
            return "scenePath不能为空";
        }
        
        // 验证场景路径格式
        if (!scenePath.EndsWith(".unity"))
        {
            return "scenePath必须以.unity结尾";
        }
        
        // 验证加载模式（如果提供）
        if (parameters.ContainsKey("loadMode"))
        {
            string loadMode = parameters["loadMode"].ToString().ToLower();
            if (loadMode != "single" && loadMode != "additive")
            {
                return "loadMode必须是'single'或'additive'";
            }
        }
        
        return null;
    }
}