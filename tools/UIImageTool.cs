using System.Collections.Generic;
using System.Net.Sockets;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

/// <summary>
/// UI Image组件工具 - 设置Image组件属性
/// </summary>
public class UIImageTool : IMCPTool
{
    public string ToolName => "ui_image_set";
    
    public string Description => "设置Image组件属性（Sprite、颜色、材质等）";
    
    public MCPResponse Execute(Dictionary<string, object> parameters, TcpClient client)
    {
        try
        {
            // 获取必需参数
            if (!parameters.ContainsKey("instanceId"))
            {
                return MCPResponse.Error("缺少必需参数: instanceId");
            }
            
            int instanceId = System.Convert.ToInt32(parameters["instanceId"]);
            
            // 获取GameObject
            GameObject gameObject = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
            if (gameObject == null)
            {
                return MCPResponse.Error($"未找到GameObject (InstanceID: {instanceId})");
            }
            
            // 获取Image组件
            Image image = gameObject.GetComponent<Image>();
            if (image == null)
            {
                return MCPResponse.Error($"GameObject '{gameObject.name}' 没有Image组件");
            }
            
            // 记录Undo操作
            Undo.RecordObject(image, "Set Image Properties");
            
            // 设置Sprite
            if (parameters.ContainsKey("spritePath"))
            {
                string spritePath = parameters["spritePath"].ToString();
                if (!string.IsNullOrEmpty(spritePath))
                {
                    Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                    if (sprite != null)
                    {
                        image.sprite = sprite;
                        Debug.Log($"设置 '{gameObject.name}' 的 sprite: {spritePath}");
                    }
                    else
                    {
                        Debug.LogWarning($"未找到Sprite资源: {spritePath}");
                    }
                }
            }
            
            // 设置颜色
            if (parameters.ContainsKey("color"))
            {
                var colorDict = parameters["color"] as Dictionary<string, object>;
                if (colorDict != null)
                {
                    Color color = new Color(
                        colorDict.ContainsKey("r") ? System.Convert.ToSingle(colorDict["r"]) : image.color.r,
                        colorDict.ContainsKey("g") ? System.Convert.ToSingle(colorDict["g"]) : image.color.g,
                        colorDict.ContainsKey("b") ? System.Convert.ToSingle(colorDict["b"]) : image.color.b,
                        colorDict.ContainsKey("a") ? System.Convert.ToSingle(colorDict["a"]) : image.color.a
                    );
                    image.color = color;
                    Debug.Log($"设置 '{gameObject.name}' 的 color: {color}");
                }
            }
            
            // 设置材质
            if (parameters.ContainsKey("material"))
            {
                string materialPath = parameters["material"].ToString();
                if (!string.IsNullOrEmpty(materialPath))
                {
                    Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if (material != null)
                    {
                        image.material = material;
                        Debug.Log($"设置 '{gameObject.name}' 的 material: {materialPath}");
                    }
                    else
                    {
                        Debug.LogWarning($"未找到Material资源: {materialPath}");
                    }
                }
            }
            
            // 设置Image类型
            if (parameters.ContainsKey("imageType"))
            {
                string imageTypeStr = parameters["imageType"].ToString();
                if (System.Enum.TryParse<Image.Type>(imageTypeStr, out Image.Type imageType))
                {
                    image.type = imageType;
                    Debug.Log($"设置 '{gameObject.name}' 的 imageType: {imageType}");
                }
                else
                {
                    Debug.LogWarning($"无效的图片类型: {imageTypeStr}");
                }
            }
            
            // 设置保持宽高比
            if (parameters.ContainsKey("preserveAspect"))
            {
                bool preserveAspect = System.Convert.ToBoolean(parameters["preserveAspect"]);
                image.preserveAspect = preserveAspect;
                Debug.Log($"设置 '{gameObject.name}' 的 preserveAspect: {preserveAspect}");
            }
            
            // 设置填充方式（仅对Filled类型有效）
            if (parameters.ContainsKey("fillMethod") && image.type == Image.Type.Filled)
            {
                string fillMethodStr = parameters["fillMethod"].ToString();
                if (System.Enum.TryParse<Image.FillMethod>(fillMethodStr, out Image.FillMethod fillMethod))
                {
                    image.fillMethod = fillMethod;
                    Debug.Log($"设置 '{gameObject.name}' 的 fillMethod: {fillMethod}");
                }
                else
                {
                    Debug.LogWarning($"无效的填充方式: {fillMethodStr}");
                }
            }
            
            // 设置填充量（仅对Filled类型有效）
            if (parameters.ContainsKey("fillAmount") && image.type == Image.Type.Filled)
            {
                float fillAmount = System.Convert.ToSingle(parameters["fillAmount"]);
                fillAmount = Mathf.Clamp01(fillAmount);
                image.fillAmount = fillAmount;
                Debug.Log($"设置 '{gameObject.name}' 的 fillAmount: {fillAmount}");
            }
            
            // 设置使用Sprite网格（仅对Sliced类型有效）
            if (parameters.ContainsKey("useSpriteMesh") && image.type == Image.Type.Sliced)
            {
                bool useSpriteMesh = System.Convert.ToBoolean(parameters["useSpriteMesh"]);
                image.useSpriteMesh = useSpriteMesh;
                Debug.Log($"设置 '{gameObject.name}' 的 useSpriteMesh: {useSpriteMesh}");
            }
            
            // 设置Alpha命中测试最小阈值
            if (parameters.ContainsKey("alphaHitTestMinimumThreshold"))
            {
                float threshold = System.Convert.ToSingle(parameters["alphaHitTestMinimumThreshold"]);
                threshold = Mathf.Clamp01(threshold);
                image.alphaHitTestMinimumThreshold = threshold;
                Debug.Log($"设置 '{gameObject.name}' 的 alphaHitTestMinimumThreshold: {threshold}");
            }
            
            // 返回设置后的Image信息
            var result = new Dictionary<string, object>
            {
                ["name"] = gameObject.name,
                ["instanceId"] = instanceId,
                ["sprite"] = image.sprite != null ? new Dictionary<string, object>
                {
                    ["name"] = image.sprite.name,
                    ["path"] = AssetDatabase.GetAssetPath(image.sprite)
                } : null,
                ["color"] = new Dictionary<string, float>
                {
                    ["r"] = image.color.r,
                    ["g"] = image.color.g,
                    ["b"] = image.color.b,
                    ["a"] = image.color.a
                },
                ["material"] = image.material != null ? new Dictionary<string, object>
                {
                    ["name"] = image.material.name,
                    ["path"] = AssetDatabase.GetAssetPath(image.material)
                } : null,
                ["imageType"] = image.type.ToString(),
                ["preserveAspect"] = image.preserveAspect,
                ["alphaHitTestMinimumThreshold"] = image.alphaHitTestMinimumThreshold
            };
            
            // 添加Filled类型特有的属性
            if (image.type == Image.Type.Filled)
            {
                result["fillMethod"] = image.fillMethod.ToString();
                result["fillAmount"] = image.fillAmount;
            }
            
            // 添加Sliced类型特有的属性
            if (image.type == Image.Type.Sliced)
            {
                result["useSpriteMesh"] = image.useSpriteMesh;
            }
            
            Debug.Log($"成功设置UI元素 '{gameObject.name}' 的Image组件属性");
            
            return MCPResponse.Success(result);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"设置Image组件属性时出错: {e.Message}");
            return MCPResponse.Error($"设置Image组件属性失败: {e.Message}");
        }
    }
    
    public string ValidateParameters(Dictionary<string, object> parameters)
    {
        // 检查必需参数
        if (!parameters.ContainsKey("instanceId"))
        {
            return "缺少必需参数: instanceId";
        }
        
        // 验证instanceId是否为有效数字
        if (!int.TryParse(parameters["instanceId"].ToString(), out _))
        {
            return "instanceId必须是有效的整数";
        }
        
        return null;
    }
}