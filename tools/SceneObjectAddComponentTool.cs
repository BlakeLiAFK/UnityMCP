using System;
using System.Collections.Generic;
using System.Net.Sockets;
using UnityEngine;
using UnityEditor;

/// <summary>
/// 场景对象添加组件工具 - 给指定的GameObject添加组件
/// </summary>
public class SceneObjectAddComponentTool : IMCPTool
{
    public string ToolName => "scene_object_add_component";
    
    public string Description => "给场景中的GameObject添加指定组件";
    
    public MCPResponse Execute(Dictionary<string, object> parameters, TcpClient client)
    {
        try
        {
            int instanceId = System.Convert.ToInt32(parameters["instanceId"]);
            string componentType = parameters["componentType"].ToString();
            
            // 通过InstanceID查找GameObject
            GameObject targetObject = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
            if (targetObject == null)
            {
                return MCPResponse.Error($"未找到GameObject (InstanceID: {instanceId})");
            }
            
            // 解析组件类型
            Type compType = GetComponentType(componentType);
            if (compType == null)
            {
                return MCPResponse.Error($"未知的组件类型: {componentType}");
            }
            
            // 检查是否已经存在该组件
            if (targetObject.GetComponent(compType) != null)
            {
                return MCPResponse.Error($"对象 '{targetObject.name}' 已经包含组件 '{componentType}'");
            }
            
            // 添加组件
            Component newComponent = targetObject.AddComponent(compType);
            
            // 注册到Undo系统
            Undo.RegisterCreatedObjectUndo(newComponent, $"Add {componentType}");
            
            // 设置组件参数（如果提供）
            if (parameters.ContainsKey("properties"))
            {
                var properties = parameters["properties"] as Dictionary<string, object>;
                if (properties != null)
                {
                    SetComponentProperties(newComponent, properties);
                }
            }
            
            var result = new Dictionary<string, object>
            {
                ["gameObjectName"] = targetObject.name,
                ["gameObjectInstanceId"] = targetObject.GetInstanceID(),
                ["componentType"] = compType.Name,
                ["componentFullType"] = compType.FullName,
                ["componentInstanceId"] = newComponent.GetInstanceID(),
                ["enabled"] = newComponent is Behaviour behaviour ? behaviour.enabled : true
            };
            
            Debug.Log($"成功为对象 '{targetObject.name}' 添加组件 '{componentType}'");
            
            return MCPResponse.Success(result);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"添加组件时出错: {e.Message}");
            return MCPResponse.Error($"添加组件失败: {e.Message}");
        }
    }
    
    /// <summary>
    /// 根据字符串获取组件类型
    /// </summary>
    private Type GetComponentType(string typeName)
    {
        // 常用组件映射
        var componentMap = new Dictionary<string, Type>
        {
            ["Rigidbody"] = typeof(Rigidbody),
            ["BoxCollider"] = typeof(BoxCollider),
            ["SphereCollider"] = typeof(SphereCollider),
            ["CapsuleCollider"] = typeof(CapsuleCollider),
            ["MeshCollider"] = typeof(MeshCollider),
            ["MeshRenderer"] = typeof(MeshRenderer),
            ["MeshFilter"] = typeof(MeshFilter),
            ["Light"] = typeof(Light),
            ["Camera"] = typeof(Camera),
            ["AudioSource"] = typeof(AudioSource),
            ["AudioListener"] = typeof(AudioListener),
            ["ParticleSystem"] = typeof(ParticleSystem),
            ["Animator"] = typeof(Animator),
            ["Animation"] = typeof(Animation),
            ["Canvas"] = typeof(Canvas),
            ["CanvasRenderer"] = typeof(CanvasRenderer),
            ["Image"] = typeof(UnityEngine.UI.Image),
            ["Text"] = typeof(UnityEngine.UI.Text),
            ["Button"] = typeof(UnityEngine.UI.Button),
            ["Slider"] = typeof(UnityEngine.UI.Slider),
            ["Toggle"] = typeof(UnityEngine.UI.Toggle),
            ["InputField"] = typeof(UnityEngine.UI.InputField),
            ["ScrollRect"] = typeof(UnityEngine.UI.ScrollRect),
            ["VerticalLayoutGroup"] = typeof(UnityEngine.UI.VerticalLayoutGroup),
            ["HorizontalLayoutGroup"] = typeof(UnityEngine.UI.HorizontalLayoutGroup),
            ["GridLayoutGroup"] = typeof(UnityEngine.UI.GridLayoutGroup),
            ["ContentSizeFitter"] = typeof(UnityEngine.UI.ContentSizeFitter)
        };
        
        // 先查找预定义映射
        if (componentMap.ContainsKey(typeName))
        {
            return componentMap[typeName];
        }
        
        // 尝试通过Type.GetType查找
        Type type = Type.GetType(typeName);
        if (type != null && typeof(Component).IsAssignableFrom(type))
        {
            return type;
        }
        
        // 在UnityEngine程序集中搜索
        type = Type.GetType($"UnityEngine.{typeName}, UnityEngine");
        if (type != null && typeof(Component).IsAssignableFrom(type))
        {
            return type;
        }
        
        // 在UnityEngine.UI程序集中搜索
        type = Type.GetType($"UnityEngine.UI.{typeName}, UnityEngine.UI");
        if (type != null && typeof(Component).IsAssignableFrom(type))
        {
            return type;
        }
        
        return null;
    }
    
    /// <summary>
    /// 设置组件属性
    /// </summary>
    private void SetComponentProperties(Component component, Dictionary<string, object> properties)
    {
        try
        {
            Type componentType = component.GetType();
            
            foreach (var prop in properties)
            {
                var propertyInfo = componentType.GetProperty(prop.Key);
                if (propertyInfo != null && propertyInfo.CanWrite)
                {
                    try
                    {
                        object value = ConvertValue(prop.Value, propertyInfo.PropertyType);
                        propertyInfo.SetValue(component, value);
                        Debug.Log($"设置属性 {prop.Key} = {value}");
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"设置属性 {prop.Key} 失败: {e.Message}");
                    }
                }
                else
                {
                    var fieldInfo = componentType.GetField(prop.Key);
                    if (fieldInfo != null)
                    {
                        try
                        {
                            object value = ConvertValue(prop.Value, fieldInfo.FieldType);
                            fieldInfo.SetValue(component, value);
                            Debug.Log($"设置字段 {prop.Key} = {value}");
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning($"设置字段 {prop.Key} 失败: {e.Message}");
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"设置组件属性时出错: {e.Message}");
        }
    }
    
    /// <summary>
    /// 转换值类型
    /// </summary>
    private object ConvertValue(object value, Type targetType)
    {
        if (value == null) return null;
        
        if (targetType == typeof(Vector3))
        {
            var dict = value as Dictionary<string, object>;
            if (dict != null)
            {
                return new Vector3(
                    dict.ContainsKey("x") ? Convert.ToSingle(dict["x"]) : 0f,
                    dict.ContainsKey("y") ? Convert.ToSingle(dict["y"]) : 0f,
                    dict.ContainsKey("z") ? Convert.ToSingle(dict["z"]) : 0f
                );
            }
        }
        else if (targetType == typeof(Vector2))
        {
            var dict = value as Dictionary<string, object>;
            if (dict != null)
            {
                return new Vector2(
                    dict.ContainsKey("x") ? Convert.ToSingle(dict["x"]) : 0f,
                    dict.ContainsKey("y") ? Convert.ToSingle(dict["y"]) : 0f
                );
            }
        }
        else if (targetType == typeof(Color))
        {
            var dict = value as Dictionary<string, object>;
            if (dict != null)
            {
                return new Color(
                    dict.ContainsKey("r") ? Convert.ToSingle(dict["r"]) : 1f,
                    dict.ContainsKey("g") ? Convert.ToSingle(dict["g"]) : 1f,
                    dict.ContainsKey("b") ? Convert.ToSingle(dict["b"]) : 1f,
                    dict.ContainsKey("a") ? Convert.ToSingle(dict["a"]) : 1f
                );
            }
        }
        
        return Convert.ChangeType(value, targetType);
    }
    
    public string ValidateParameters(Dictionary<string, object> parameters)
    {
        if (parameters == null)
        {
            return "参数不能为空";
        }
        
        if (!parameters.ContainsKey("instanceId"))
        {
            return "缺少必需参数: instanceId";
        }
        
        if (!parameters.ContainsKey("componentType") || string.IsNullOrEmpty(parameters["componentType"].ToString()))
        {
            return "缺少必需参数: componentType";
        }
        
        return null;
    }
}
