using System.Collections.Generic;
using System.Net.Sockets;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

/// <summary>
/// UI Text组件工具 - 设置Text组件属性
/// </summary>
public class UITextTool : IMCPTool
{
    public string ToolName => "ui_text_set";
    
    public string Description => "设置Text组件属性（文本内容、字体、颜色等）";
    
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
            
            // 获取Text组件
            Text text = gameObject.GetComponent<Text>();
            if (text == null)
            {
                return MCPResponse.Error($"GameObject '{gameObject.name}' 没有Text组件");
            }
            
            // 记录Undo操作
            Undo.RecordObject(text, "Set Text Properties");
            
            // 设置文本内容
            if (parameters.ContainsKey("text"))
            {
                string textContent = parameters["text"].ToString();
                text.text = textContent;
                Debug.Log($"设置 '{gameObject.name}' 的文本内容: {textContent}");
            }
            
            // 设置字体大小
            if (parameters.ContainsKey("fontSize"))
            {
                int fontSize = System.Convert.ToInt32(parameters["fontSize"]);
                text.fontSize = fontSize;
                Debug.Log($"设置 '{gameObject.name}' 的字体大小: {fontSize}");
            }
            
            // 设置颜色
            if (parameters.ContainsKey("color"))
            {
                var colorDict = parameters["color"] as Dictionary<string, object>;
                if (colorDict != null)
                {
                    Color color = new Color(
                        colorDict.ContainsKey("r") ? System.Convert.ToSingle(colorDict["r"]) : text.color.r,
                        colorDict.ContainsKey("g") ? System.Convert.ToSingle(colorDict["g"]) : text.color.g,
                        colorDict.ContainsKey("b") ? System.Convert.ToSingle(colorDict["b"]) : text.color.b,
                        colorDict.ContainsKey("a") ? System.Convert.ToSingle(colorDict["a"]) : text.color.a
                    );
                    text.color = color;
                    Debug.Log($"设置 '{gameObject.name}' 的颜色: {color}");
                }
            }
            
            // 设置字体
            if (parameters.ContainsKey("fontPath"))
            {
                string fontPath = parameters["fontPath"].ToString();
                if (!string.IsNullOrEmpty(fontPath))
                {
                    Font font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
                    if (font != null)
                    {
                        text.font = font;
                        Debug.Log($"设置 '{gameObject.name}' 的字体: {fontPath}");
                    }
                    else
                    {
                        Debug.LogWarning($"未找到字体资源: {fontPath}");
                    }
                }
            }
            
            // 设置对齐方式
            if (parameters.ContainsKey("alignment"))
            {
                string alignmentStr = parameters["alignment"].ToString();
                if (System.Enum.TryParse<TextAnchor>(alignmentStr, out TextAnchor alignment))
                {
                    text.alignment = alignment;
                    Debug.Log($"设置 '{gameObject.name}' 的对齐方式: {alignment}");
                }
                else
                {
                    Debug.LogWarning($"无效的对齐方式: {alignmentStr}");
                }
            }
            
            // 设置行间距
            if (parameters.ContainsKey("lineSpacing"))
            {
                float lineSpacing = System.Convert.ToSingle(parameters["lineSpacing"]);
                text.lineSpacing = lineSpacing;
                Debug.Log($"设置 '{gameObject.name}' 的行间距: {lineSpacing}");
            }
            
            // 设置是否启用富文本
            if (parameters.ContainsKey("richText"))
            {
                bool richText = System.Convert.ToBoolean(parameters["richText"]);
                text.supportRichText = richText;
                Debug.Log($"设置 '{gameObject.name}' 的富文本支持: {richText}");
            }
            
            // 设置字体样式
            if (parameters.ContainsKey("fontStyle"))
            {
                string fontStyleStr = parameters["fontStyle"].ToString();
                if (System.Enum.TryParse<FontStyle>(fontStyleStr, out FontStyle fontStyle))
                {
                    text.fontStyle = fontStyle;
                    Debug.Log($"设置 '{gameObject.name}' 的字体样式: {fontStyle}");
                }
                else
                {
                    Debug.LogWarning($"无效的字体样式: {fontStyleStr}");
                }
            }
            
            // 设置水平溢出处理
            if (parameters.ContainsKey("horizontalOverflow"))
            {
                string horizontalOverflowStr = parameters["horizontalOverflow"].ToString();
                if (System.Enum.TryParse<HorizontalWrapMode>(horizontalOverflowStr, out HorizontalWrapMode horizontalOverflow))
                {
                    text.horizontalOverflow = horizontalOverflow;
                    Debug.Log($"设置 '{gameObject.name}' 的水平溢出处理: {horizontalOverflow}");
                }
                else
                {
                    Debug.LogWarning($"无效的水平溢出处理: {horizontalOverflowStr}");
                }
            }
            
            // 设置垂直溢出处理
            if (parameters.ContainsKey("verticalOverflow"))
            {
                string verticalOverflowStr = parameters["verticalOverflow"].ToString();
                if (System.Enum.TryParse<VerticalWrapMode>(verticalOverflowStr, out VerticalWrapMode verticalOverflow))
                {
                    text.verticalOverflow = verticalOverflow;
                    Debug.Log($"设置 '{gameObject.name}' 的垂直溢出处理: {verticalOverflow}");
                }
                else
                {
                    Debug.LogWarning($"无效的垂直溢出处理: {verticalOverflowStr}");
                }
            }
            
            // 设置最佳尺寸
            if (parameters.ContainsKey("resizeTextForBestFit"))
            {
                bool resizeTextForBestFit = System.Convert.ToBoolean(parameters["resizeTextForBestFit"]);
                text.resizeTextForBestFit = resizeTextForBestFit;
                Debug.Log($"设置 '{gameObject.name}' 的最佳尺寸: {resizeTextForBestFit}");
            }
            
            // 设置最小字体大小（仅在启用最佳尺寸时有效）
            if (parameters.ContainsKey("resizeTextMinSize") && text.resizeTextForBestFit)
            {
                int minSize = System.Convert.ToInt32(parameters["resizeTextMinSize"]);
                text.resizeTextMinSize = minSize;
                Debug.Log($"设置 '{gameObject.name}' 的最小字体大小: {minSize}");
            }
            
            // 设置最大字体大小（仅在启用最佳尺寸时有效）
            if (parameters.ContainsKey("resizeTextMaxSize") && text.resizeTextForBestFit)
            {
                int maxSize = System.Convert.ToInt32(parameters["resizeTextMaxSize"]);
                text.resizeTextMaxSize = maxSize;
                Debug.Log($"设置 '{gameObject.name}' 的最大字体大小: {maxSize}");
            }
            
            // 返回设置后的Text信息
            var result = new Dictionary<string, object>
            {
                ["name"] = gameObject.name,
                ["instanceId"] = instanceId,
                ["text"] = text.text,
                ["fontSize"] = text.fontSize,
                ["color"] = new Dictionary<string, float>
                {
                    ["r"] = text.color.r,
                    ["g"] = text.color.g,
                    ["b"] = text.color.b,
                    ["a"] = text.color.a
                },
                ["font"] = text.font != null ? new Dictionary<string, object>
                {
                    ["name"] = text.font.name,
                    ["path"] = AssetDatabase.GetAssetPath(text.font)
                } : null,
                ["alignment"] = text.alignment.ToString(),
                ["lineSpacing"] = text.lineSpacing,
                ["richText"] = text.supportRichText,
                ["fontStyle"] = text.fontStyle.ToString(),
                ["horizontalOverflow"] = text.horizontalOverflow.ToString(),
                ["verticalOverflow"] = text.verticalOverflow.ToString(),
                ["resizeTextForBestFit"] = text.resizeTextForBestFit,
                ["resizeTextMinSize"] = text.resizeTextMinSize,
                ["resizeTextMaxSize"] = text.resizeTextMaxSize
            };
            
            Debug.Log($"成功设置UI元素 '{gameObject.name}' 的Text组件属性");
            
            return MCPResponse.Success(result);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"设置Text组件属性时出错: {e.Message}");
            return MCPResponse.Error($"设置Text组件属性失败: {e.Message}");
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