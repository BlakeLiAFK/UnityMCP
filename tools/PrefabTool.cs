using System.Collections.Generic;
using System.Net.Sockets;
using UnityEngine;
using UnityEditor;

/// <summary>
/// 预制体实例化工具 - 实例化预制体到场景中
/// </summary>
public class PrefabTool : IMCPTool
{
    public string ToolName => "prefab_instantiate";
    
    public string Description => "实例化预制体到场景中";
    
    public MCPResponse Execute(Dictionary<string, object> parameters, TcpClient client)
    {
        try
        {
            // 获取必需参数
            if (!parameters.ContainsKey("prefabPath"))
            {
                return MCPResponse.Error("缺少必需参数: prefabPath");
            }
            
            string prefabPath = parameters["prefabPath"].ToString();
            
            // 加载预制体
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabAsset == null)
            {
                return MCPResponse.Error($"无法加载预制体: {prefabPath}");
            }
            
            // 实例化预制体
            GameObject instance = PrefabUtility.InstantiatePrefab(prefabAsset) as GameObject;
            if (instance == null)
            {
                return MCPResponse.Error($"实例化预制体失败: {prefabPath}");
            }
            
            // 设置父对象
            if (parameters.ContainsKey("parentId"))
            {
                int parentId = System.Convert.ToInt32(parameters["parentId"]);
                GameObject parentObject = EditorUtility.InstanceIDToObject(parentId) as GameObject;
                if (parentObject != null)
                {
                    instance.transform.SetParent(parentObject.transform);
                    Debug.Log($"将预制体实例设置为 '{parentObject.name}' 的子对象");
                }
                else
                {
                    Debug.LogWarning($"未找到父对象 (InstanceID: {parentId})");
                }
            }
            
            // 设置位置
            if (parameters.ContainsKey("position"))
            {
                var posDict = parameters["position"] as Dictionary<string, object>;
                if (posDict != null)
                {
                    Vector3 position = new Vector3(
                        posDict.ContainsKey("x") ? System.Convert.ToSingle(posDict["x"]) : 0f,
                        posDict.ContainsKey("y") ? System.Convert.ToSingle(posDict["y"]) : 0f,
                        posDict.ContainsKey("z") ? System.Convert.ToSingle(posDict["z"]) : 0f
                    );
                    instance.transform.position = position;
                    Debug.Log($"设置预制体实例位置: {position}");
                }
            }
            
            // 设置旋转
            if (parameters.ContainsKey("rotation"))
            {
                var rotDict = parameters["rotation"] as Dictionary<string, object>;
                if (rotDict != null)
                {
                    Vector3 eulerAngles = new Vector3(
                        rotDict.ContainsKey("x") ? System.Convert.ToSingle(rotDict["x"]) : 0f,
                        rotDict.ContainsKey("y") ? System.Convert.ToSingle(rotDict["y"]) : 0f,
                        rotDict.ContainsKey("z") ? System.Convert.ToSingle(rotDict["z"]) : 0f
                    );
                    instance.transform.rotation = Quaternion.Euler(eulerAngles);
                    Debug.Log($"设置预制体实例旋转: {eulerAngles}");
                }
            }
            
            // 设置缩放
            if (parameters.ContainsKey("scale"))
            {
                var scaleDict = parameters["scale"] as Dictionary<string, object>;
                if (scaleDict != null)
                {
                    Vector3 scale = new Vector3(
                        scaleDict.ContainsKey("x") ? System.Convert.ToSingle(scaleDict["x"]) : 1f,
                        scaleDict.ContainsKey("y") ? System.Convert.ToSingle(scaleDict["y"]) : 1f,
                        scaleDict.ContainsKey("z") ? System.Convert.ToSingle(scaleDict["z"]) : 1f
                    );
                    instance.transform.localScale = scale;
                    Debug.Log($"设置预制体实例缩放: {scale}");
                }
            }
            
            // 注册到Undo系统
            Undo.RegisterCreatedObjectUndo(instance, $"Instantiate Prefab {prefabAsset.name}");
            
            // 选中实例化的对象
            Selection.activeGameObject = instance;
            
            // 获取预制体连接状态
            PrefabInstanceStatus prefabStatus = PrefabUtility.GetPrefabInstanceStatus(instance);
            
            var result = new Dictionary<string, object>
            {
                ["name"] = instance.name,
                ["instanceId"] = instance.GetInstanceID(),
                ["prefabPath"] = prefabPath,
                ["prefabStatus"] = prefabStatus.ToString(),
                ["position"] = new Dictionary<string, float>
                {
                    ["x"] = instance.transform.position.x,
                    ["y"] = instance.transform.position.y,
                    ["z"] = instance.transform.position.z
                },
                ["rotation"] = new Dictionary<string, float>
                {
                    ["x"] = instance.transform.rotation.eulerAngles.x,
                    ["y"] = instance.transform.rotation.eulerAngles.y,
                    ["z"] = instance.transform.rotation.eulerAngles.z
                },
                ["scale"] = new Dictionary<string, float>
                {
                    ["x"] = instance.transform.localScale.x,
                    ["y"] = instance.transform.localScale.y,
                    ["z"] = instance.transform.localScale.z
                }
            };
            
            // 添加父对象信息
            if (instance.transform.parent != null)
            {
                result["parent"] = new Dictionary<string, object>
                {
                    ["name"] = instance.transform.parent.name,
                    ["instanceId"] = instance.transform.parent.GetInstanceID()
                };
            }
            
            Debug.Log($"成功实例化预制体: {prefabPath} -> {instance.name} (InstanceID: {instance.GetInstanceID()})");
            
            return MCPResponse.Success(result);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"实例化预制体时出错: {e.Message}");
            return MCPResponse.Error($"实例化预制体失败: {e.Message}");
        }
    }
    
    public string ValidateParameters(Dictionary<string, object> parameters)
    {
        // 检查必需参数
        if (!parameters.ContainsKey("prefabPath"))
        {
            return "缺少必需参数: prefabPath";
        }
        
        string prefabPath = parameters["prefabPath"].ToString();
        if (string.IsNullOrEmpty(prefabPath))
        {
            return "prefabPath不能为空";
        }
        
        return null;
    }
}