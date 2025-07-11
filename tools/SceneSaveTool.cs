using System.Collections.Generic;
using System.Net.Sockets;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// 场景保存工具 - 保存当前场景或指定场景
/// </summary>
public class SceneSaveTool : IMCPTool
{
    public string ToolName => "scene_save";
    
    public string Description => "保存当前场景或指定场景文件";
    
    public MCPResponse Execute(Dictionary<string, object> parameters, TcpClient client)
    {
        try
        {
            string scenePath = parameters.ContainsKey("scenePath") ? parameters["scenePath"].ToString() : "";
            bool saveAsNew = parameters.ContainsKey("saveAsNew") ? System.Convert.ToBoolean(parameters["saveAsNew"]) : false;
            bool saveAll = parameters.ContainsKey("saveAll") ? System.Convert.ToBoolean(parameters["saveAll"]) : false;
            
            var result = new Dictionary<string, object>
            {
                ["saveAsNew"] = saveAsNew,
                ["saveAll"] = saveAll,
                ["timestamp"] = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };
            
            if (saveAll)
            {
                // 保存所有打开的场景
                return SaveAllScenes(result);
            }
            else if (!string.IsNullOrEmpty(scenePath))
            {
                // 保存指定路径的场景
                return SaveSceneToPath(scenePath, saveAsNew, result);
            }
            else
            {
                // 保存当前活动场景
                return SaveActiveScene(saveAsNew, result);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"保存场景时出错: {e.Message}");
            return MCPResponse.Error($"保存场景失败: {e.Message}");
        }
    }
    
    /// <summary>
    /// 保存当前活动场景
    /// </summary>
    private MCPResponse SaveActiveScene(bool saveAsNew, Dictionary<string, object> result)
    {
        try
        {
            Scene activeScene = SceneManager.GetActiveScene();
            
            if (!activeScene.IsValid())
            {
                return MCPResponse.Error("没有有效的活动场景");
            }
            
            result["sceneName"] = activeScene.name;
            result["originalPath"] = activeScene.path;
            
            bool success;
            string finalPath;
            
            if (saveAsNew || string.IsNullOrEmpty(activeScene.path))
            {
                // 另存为新场景
                string savePath = EditorUtility.SaveFilePanelInProject(
                    "保存场景",
                    activeScene.name,
                    "unity",
                    "请选择保存场景的位置");
                
                if (string.IsNullOrEmpty(savePath))
                {
                    return MCPResponse.Error("用户取消了保存操作");
                }
                
                success = EditorSceneManager.SaveScene(activeScene, savePath);
                finalPath = savePath;
            }
            else
            {
                // 保存到原路径
                success = EditorSceneManager.SaveScene(activeScene);
                finalPath = activeScene.path;
            }
            
            if (success)
            {
                result["success"] = true;
                result["savedPath"] = finalPath;
                result["isDirty"] = activeScene.isDirty;
                result["message"] = $"成功保存场景: {finalPath}";
                
                Debug.Log($"成功保存场景: {activeScene.name} -> {finalPath}");
                
                return MCPResponse.Success(result);
            }
            else
            {
                return MCPResponse.Error($"保存场景失败: {activeScene.name}");
            }
        }
        catch (System.Exception e)
        {
            return MCPResponse.Error($"保存活动场景失败: {e.Message}");
        }
    }
    
    /// <summary>
    /// 保存到指定路径
    /// </summary>
    private MCPResponse SaveSceneToPath(string scenePath, bool saveAsNew, Dictionary<string, object> result)
    {
        try
        {
            Scene activeScene = SceneManager.GetActiveScene();
            
            if (!activeScene.IsValid())
            {
                return MCPResponse.Error("没有有效的活动场景");
            }
            
            // 确保路径以.unity结尾
            if (!scenePath.EndsWith(".unity"))
            {
                scenePath += ".unity";
            }
            
            // 确保路径在Assets目录下
            if (!scenePath.StartsWith("Assets/"))
            {
                scenePath = "Assets/" + scenePath;
            }
            
            result["sceneName"] = activeScene.name;
            result["originalPath"] = activeScene.path;
            result["targetPath"] = scenePath;
            
            // 检查目标文件是否存在
            if (!saveAsNew && System.IO.File.Exists(scenePath))
            {
                if (!EditorUtility.DisplayDialog("确认覆盖", 
                    $"场景文件 '{scenePath}' 已存在。是否要覆盖？", 
                    "覆盖", "取消"))
                {
                    return MCPResponse.Error("用户取消了覆盖操作");
                }
            }
            
            // 确保目录存在
            string directory = System.IO.Path.GetDirectoryName(scenePath);
            if (!System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
                AssetDatabase.Refresh();
            }
            
            bool success = EditorSceneManager.SaveScene(activeScene, scenePath);
            
            if (success)
            {
                result["success"] = true;
                result["savedPath"] = scenePath;
                result["message"] = $"成功保存场景到: {scenePath}";
                
                Debug.Log($"成功保存场景: {activeScene.name} -> {scenePath}");
                
                return MCPResponse.Success(result);
            }
            else
            {
                return MCPResponse.Error($"保存场景到指定路径失败: {scenePath}");
            }
        }
        catch (System.Exception e)
        {
            return MCPResponse.Error($"保存场景到指定路径失败: {e.Message}");
        }
    }
    
    /// <summary>
    /// 保存所有打开的场景
    /// </summary>
    private MCPResponse SaveAllScenes(Dictionary<string, object> result)
    {
        try
        {
            int sceneCount = SceneManager.sceneCount;
            var savedScenes = new List<Dictionary<string, object>>();
            var failedScenes = new List<Dictionary<string, object>>();
            
            for (int i = 0; i < sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                
                if (!scene.IsValid())
                    continue;
                
                var sceneInfo = new Dictionary<string, object>
                {
                    ["name"] = scene.name,
                    ["path"] = scene.path,
                    ["isDirty"] = scene.isDirty,
                    ["isLoaded"] = scene.isLoaded
                };
                
                if (scene.isDirty || string.IsNullOrEmpty(scene.path))
                {
                    bool success;
                    
                    if (string.IsNullOrEmpty(scene.path))
                    {
                        // 未保存的场景，需要用户选择路径
                        string savePath = EditorUtility.SaveFilePanelInProject(
                            $"保存场景 '{scene.name}'",
                            scene.name,
                            "unity",
                            $"请为场景 '{scene.name}' 选择保存位置");
                        
                        if (string.IsNullOrEmpty(savePath))
                        {
                            sceneInfo["error"] = "用户取消保存";
                            failedScenes.Add(sceneInfo);
                            continue;
                        }
                        
                        success = EditorSceneManager.SaveScene(scene, savePath);
                        sceneInfo["savedPath"] = savePath;
                    }
                    else
                    {
                        success = EditorSceneManager.SaveScene(scene);
                        sceneInfo["savedPath"] = scene.path;
                    }
                    
                    if (success)
                    {
                        sceneInfo["success"] = true;
                        savedScenes.Add(sceneInfo);
                    }
                    else
                    {
                        sceneInfo["error"] = "保存失败";
                        failedScenes.Add(sceneInfo);
                    }
                }
                else
                {
                    sceneInfo["message"] = "场景无需保存";
                    sceneInfo["savedPath"] = scene.path;
                    savedScenes.Add(sceneInfo);
                }
            }
            
            result["totalScenes"] = sceneCount;
            result["savedScenes"] = savedScenes;
            result["failedScenes"] = failedScenes;
            result["successCount"] = savedScenes.Count;
            result["failureCount"] = failedScenes.Count;
            
            if (failedScenes.Count == 0)
            {
                result["success"] = true;
                result["message"] = $"成功保存所有 {savedScenes.Count} 个场景";
                
                Debug.Log($"成功保存所有场景，共 {savedScenes.Count} 个");
                
                return MCPResponse.Success(result);
            }
            else
            {
                result["success"] = false;
                result["message"] = $"保存了 {savedScenes.Count} 个场景，{failedScenes.Count} 个场景保存失败";
                
                return MCPResponse.Error($"部分场景保存失败：{failedScenes.Count}/{sceneCount}");
            }
        }
        catch (System.Exception e)
        {
            return MCPResponse.Error($"保存所有场景失败: {e.Message}");
        }
    }
    
    public string ValidateParameters(Dictionary<string, object> parameters)
    {
        // 所有参数都是可选的，无需验证
        return null;
    }
}