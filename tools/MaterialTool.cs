using System.Collections.Generic;
using System.Net.Sockets;
using UnityEngine;
using UnityEditor;

/// <summary>
/// 材质工具 - 设置材质属性
/// </summary>
public class MaterialTool : IMCPTool
{
    public string ToolName => "material_set_property";
    
    public string Description => "设置材质属性（Float、Color、Texture、Vector等）";
    
    public MCPResponse Execute(Dictionary<string, object> parameters, TcpClient client)
    {
        try
        {
            // 获取必需参数
            if (!parameters.ContainsKey("materialPath"))
            {
                return MCPResponse.Error("缺少必需参数: materialPath");
            }
            
            if (!parameters.ContainsKey("propertyName"))
            {
                return MCPResponse.Error("缺少必需参数: propertyName");
            }
            
            if (!parameters.ContainsKey("value"))
            {
                return MCPResponse.Error("缺少必需参数: value");
            }
            
            if (!parameters.ContainsKey("propertyType"))
            {
                return MCPResponse.Error("缺少必需参数: propertyType");
            }
            
            string materialPath = parameters["materialPath"].ToString();
            string propertyName = parameters["propertyName"].ToString();
            string propertyType = parameters["propertyType"].ToString();
            object value = parameters["value"];
            
            // 加载材质
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                return MCPResponse.Error($"无法加载材质: {materialPath}");
            }
            
            // 检查属性是否存在
            if (!material.HasProperty(propertyName))
            {
                return MCPResponse.Error($"材质 '{material.name}' 没有属性 '{propertyName}'");
            }
            
            // 记录Undo操作
            Undo.RecordObject(material, $"Set Material Property {propertyName}");
            
            // 根据属性类型设置值
            switch (propertyType.ToLower())
            {
                case "float":
                    float floatValue = System.Convert.ToSingle(value);
                    material.SetFloat(propertyName, floatValue);
                    Debug.Log($"设置材质 '{material.name}' 的Float属性 '{propertyName}' = {floatValue}");
                    break;
                    
                case "int":
                case "integer":
                    int intValue = System.Convert.ToInt32(value);
                    material.SetInt(propertyName, intValue);
                    Debug.Log($"设置材质 '{material.name}' 的Int属性 '{propertyName}' = {intValue}");
                    break;
                    
                case "color":
                    Color colorValue;
                    if (value is Dictionary<string, object> colorDict)
                    {
                        colorValue = new Color(
                            colorDict.ContainsKey("r") ? System.Convert.ToSingle(colorDict["r"]) : 0f,
                            colorDict.ContainsKey("g") ? System.Convert.ToSingle(colorDict["g"]) : 0f,
                            colorDict.ContainsKey("b") ? System.Convert.ToSingle(colorDict["b"]) : 0f,
                            colorDict.ContainsKey("a") ? System.Convert.ToSingle(colorDict["a"]) : 1f
                        );
                    }
                    else
                    {
                        return MCPResponse.Error("Color值必须是包含r,g,b,a字段的字典");
                    }
                    material.SetColor(propertyName, colorValue);
                    Debug.Log($"设置材质 '{material.name}' 的Color属性 '{propertyName}' = {colorValue}");
                    break;
                    
                case "vector":
                case "vector4":
                    Vector4 vectorValue;
                    if (value is Dictionary<string, object> vectorDict)
                    {
                        vectorValue = new Vector4(
                            vectorDict.ContainsKey("x") ? System.Convert.ToSingle(vectorDict["x"]) : 0f,
                            vectorDict.ContainsKey("y") ? System.Convert.ToSingle(vectorDict["y"]) : 0f,
                            vectorDict.ContainsKey("z") ? System.Convert.ToSingle(vectorDict["z"]) : 0f,
                            vectorDict.ContainsKey("w") ? System.Convert.ToSingle(vectorDict["w"]) : 0f
                        );
                    }
                    else
                    {
                        return MCPResponse.Error("Vector值必须是包含x,y,z,w字段的字典");
                    }
                    material.SetVector(propertyName, vectorValue);
                    Debug.Log($"设置材质 '{material.name}' 的Vector属性 '{propertyName}' = {vectorValue}");
                    break;
                    
                case "texture":
                case "texture2d":
                    string texturePath = value.ToString();
                    if (!string.IsNullOrEmpty(texturePath))
                    {
                        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                        if (texture != null)
                        {
                            material.SetTexture(propertyName, texture);
                            Debug.Log($"设置材质 '{material.name}' 的Texture属性 '{propertyName}' = {texturePath}");
                        }
                        else
                        {
                            return MCPResponse.Error($"无法加载纹理: {texturePath}");
                        }
                    }
                    else
                    {
                        material.SetTexture(propertyName, null);
                        Debug.Log($"清除材质 '{material.name}' 的Texture属性 '{propertyName}'");
                    }
                    break;
                    
                default:
                    return MCPResponse.Error($"不支持的属性类型: {propertyType}。支持的类型: Float, Int, Color, Vector, Texture");
            }
            
            // 标记材质为脏（需要保存）
            EditorUtility.SetDirty(material);
            
            // 构建返回结果
            var result = new Dictionary<string, object>
            {
                ["materialPath"] = materialPath,
                ["materialName"] = material.name,
                ["propertyName"] = propertyName,
                ["propertyType"] = propertyType,
                ["success"] = true
            };
            
            // 获取设置后的值进行验证
            try
            {
                switch (propertyType.ToLower())
                {
                    case "float":
                        result["actualValue"] = material.GetFloat(propertyName);
                        break;
                    case "int":
                    case "integer":
                        result["actualValue"] = material.GetInt(propertyName);
                        break;
                    case "color":
                        Color color = material.GetColor(propertyName);
                        result["actualValue"] = new Dictionary<string, float>
                        {
                            ["r"] = color.r,
                            ["g"] = color.g,
                            ["b"] = color.b,
                            ["a"] = color.a
                        };
                        break;
                    case "vector":
                    case "vector4":
                        Vector4 vector = material.GetVector(propertyName);
                        result["actualValue"] = new Dictionary<string, float>
                        {
                            ["x"] = vector.x,
                            ["y"] = vector.y,
                            ["z"] = vector.z,
                            ["w"] = vector.w
                        };
                        break;
                    case "texture":
                    case "texture2d":
                        Texture texture = material.GetTexture(propertyName);
                        result["actualValue"] = texture != null ? AssetDatabase.GetAssetPath(texture) : null;
                        break;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"获取材质属性值时出错: {e.Message}");
            }
            
            // 获取材质的shader信息
            if (material.shader != null)
            {
                result["shader"] = new Dictionary<string, object>
                {
                    ["name"] = material.shader.name,
                    ["path"] = AssetDatabase.GetAssetPath(material.shader)
                };
            }
            
            Debug.Log($"成功设置材质属性: {materialPath} -> {propertyName}");
            
            return MCPResponse.Success(result);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"设置材质属性时出错: {e.Message}");
            return MCPResponse.Error($"设置材质属性失败: {e.Message}");
        }
    }
    
    public string ValidateParameters(Dictionary<string, object> parameters)
    {
        // 检查必需参数
        string[] requiredParams = { "materialPath", "propertyName", "value", "propertyType" };
        
        foreach (string param in requiredParams)
        {
            if (!parameters.ContainsKey(param))
            {
                return $"缺少必需参数: {param}";
            }
            
            if (string.IsNullOrEmpty(parameters[param].ToString()))
            {
                return $"参数 {param} 不能为空";
            }
        }
        
        // 验证属性类型
        string propertyType = parameters["propertyType"].ToString().ToLower();
        string[] supportedTypes = { "float", "int", "integer", "color", "vector", "vector4", "texture", "texture2d" };
        
        bool isValidType = false;
        foreach (string type in supportedTypes)
        {
            if (propertyType == type)
            {
                isValidType = true;
                break;
            }
        }
        
        if (!isValidType)
        {
            return $"不支持的属性类型: {propertyType}。支持的类型: {string.Join(", ", supportedTypes)}";
        }
        
        return null;
    }
}