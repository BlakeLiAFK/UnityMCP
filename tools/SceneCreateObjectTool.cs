using System.Collections.Generic;
using System.Net.Sockets;
using UnityEngine;
using UnityEditor;

/// <summary>
/// 场景创建对象工具 - 在场景中创建GameObject
/// </summary>
public class SceneCreateObjectTool : IMCPTool
{
    public string ToolName => "scene_create_object";
    
    public string Description => "在当前场景中创建新的GameObject";
    
    public MCPResponse Execute(Dictionary<string, object> parameters, TcpClient client)
    {
        try
        {
            string objectName = parameters.ContainsKey("name") ? parameters["name"].ToString() : "New GameObject";
            int parentInstanceId = parameters.ContainsKey("parentId") ? System.Convert.ToInt32(parameters["parentId"]) : 0;
            
            // 创建新的GameObject
            GameObject newObject = new GameObject(objectName);
            
            // 如果指定了父对象，设置父子关系
            if (parentInstanceId != 0)
            {
                GameObject parentObject = EditorUtility.InstanceIDToObject(parentInstanceId) as GameObject;
                if (parentObject != null)
                {
                    newObject.transform.SetParent(parentObject.transform);
                    Debug.Log($"将新对象 '{objectName}' 设置为 '{parentObject.name}' 的子对象");
                }
                else
                {
                    Debug.LogWarning($"未找到父对象 (InstanceID: {parentInstanceId})");
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
                    newObject.transform.position = position;
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
                    newObject.transform.rotation = Quaternion.Euler(eulerAngles);
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
                    newObject.transform.localScale = scale;
                }
            }
            
            // 设置标签
            if (parameters.ContainsKey("tag"))
            {
                string tag = parameters["tag"].ToString();
                try
                {
                    newObject.tag = tag;
                }
                catch (UnityException)
                {
                    Debug.LogWarning($"无效的标签: {tag}，使用默认标签");
                }
            }
            
            // 设置层级
            if (parameters.ContainsKey("layer"))
            {
                int layer = System.Convert.ToInt32(parameters["layer"]);
                if (layer >= 0 && layer <= 31)
                {
                    newObject.layer = layer;
                }
                else
                {
                    Debug.LogWarning($"无效的层级: {layer}，必须在0-31之间");
                }
            }
            
            // 注册到Undo系统
            Undo.RegisterCreatedObjectUndo(newObject, $"Create {objectName}");
            
            // 选中新创建的对象
            Selection.activeGameObject = newObject;
            
            var result = new Dictionary<string, object>
            {
                ["name"] = newObject.name,
                ["instanceId"] = newObject.GetInstanceID(),
                ["position"] = new Dictionary<string, float>
                {
                    ["x"] = newObject.transform.position.x,
                    ["y"] = newObject.transform.position.y,
                    ["z"] = newObject.transform.position.z
                },
                ["rotation"] = new Dictionary<string, float>
                {
                    ["x"] = newObject.transform.rotation.eulerAngles.x,
                    ["y"] = newObject.transform.rotation.eulerAngles.y,
                    ["z"] = newObject.transform.rotation.eulerAngles.z
                },
                ["scale"] = new Dictionary<string, float>
                {
                    ["x"] = newObject.transform.localScale.x,
                    ["y"] = newObject.transform.localScale.y,
                    ["z"] = newObject.transform.localScale.z
                },
                ["tag"] = newObject.tag,
                ["layer"] = newObject.layer,
                ["layerName"] = LayerMask.LayerToName(newObject.layer)
            };
            
            Debug.Log($"成功创建GameObject: {objectName} (InstanceID: {newObject.GetInstanceID()})");
            
            return MCPResponse.Success(result);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"创建GameObject时出错: {e.Message}");
            return MCPResponse.Error($"创建GameObject失败: {e.Message}");
        }
    }
    
    public string ValidateParameters(Dictionary<string, object> parameters)
    {
        // 此工具不需要必需参数，所有参数都是可选的
        // name参数如果不提供会使用默认值
        return null;
    }
}
