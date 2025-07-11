using System.Collections.Generic;
using System.Net.Sockets;
using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// 资源信息获取工具 - 获取资源详细信息
/// </summary>
public class AssetInfoTool : IMCPTool
{
    public string ToolName => "asset_get_info";
    
    public string Description => "获取资源详细信息（元数据、导入设置等）";
    
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
            bool includeMetadata = parameters.ContainsKey("includeMetadata") ? 
                System.Convert.ToBoolean(parameters["includeMetadata"]) : true;
            bool includeImportSettings = parameters.ContainsKey("includeImportSettings") ? 
                System.Convert.ToBoolean(parameters["includeImportSettings"]) : false;
            
            // 验证资源路径
            if (!File.Exists(assetPath) && !AssetDatabase.LoadMainAssetAtPath(assetPath))
            {
                return MCPResponse.Error($"资源路径不存在: {assetPath}");
            }
            
            // 获取基本信息
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            var result = new Dictionary<string, object>
            {
                ["guid"] = guid,
                ["path"] = assetPath,
                ["name"] = Path.GetFileNameWithoutExtension(assetPath),
                ["extension"] = Path.GetExtension(assetPath).TrimStart('.'),
                ["directory"] = Path.GetDirectoryName(assetPath)
            };
            
            // 获取资源类型信息
            System.Type mainAssetType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
            if (mainAssetType != null)
            {
                result["mainAssetType"] = new Dictionary<string, object>
                {
                    ["name"] = mainAssetType.Name,
                    ["fullName"] = mainAssetType.FullName,
                    ["namespace"] = mainAssetType.Namespace
                };
            }
            
            // 获取所有子资源
            UnityEngine.Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            if (subAssets.Length > 1) // 除了主资源外还有子资源
            {
                var subAssetList = new List<Dictionary<string, object>>();
                foreach (var subAsset in subAssets)
                {
                    if (subAsset != null && !AssetDatabase.IsMainAsset(subAsset))
                    {
                        subAssetList.Add(new Dictionary<string, object>
                        {
                            ["name"] = subAsset.name,
                            ["type"] = subAsset.GetType().Name,
                            ["instanceId"] = subAsset.GetInstanceID()
                        });
                    }
                }
                
                if (subAssetList.Count > 0)
                {
                    result["subAssets"] = subAssetList;
                }
            }
            
            // 获取文件系统信息
            string fullPath = Path.GetFullPath(assetPath);
            if (File.Exists(fullPath))
            {
                FileInfo fileInfo = new FileInfo(fullPath);
                result["fileInfo"] = new Dictionary<string, object>
                {
                    ["size"] = fileInfo.Length,
                    ["sizeFormatted"] = FormatFileSize(fileInfo.Length),
                    ["created"] = fileInfo.CreationTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    ["lastModified"] = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    ["lastAccessed"] = fileInfo.LastAccessTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    ["isReadOnly"] = fileInfo.IsReadOnly
                };
            }
            
            // 获取资源标签和AssetBundle信息
            UnityEngine.Object mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (mainAsset != null)
            {
                string[] labels = AssetDatabase.GetLabels(mainAsset);
                if (labels.Length > 0)
                {
                    result["labels"] = labels;
                }
                
                string bundleName = AssetDatabase.GetImplicitAssetBundleName(assetPath);
                if (!string.IsNullOrEmpty(bundleName))
                {
                    result["assetBundle"] = new Dictionary<string, object>
                    {
                        ["name"] = bundleName,
                        ["variant"] = AssetDatabase.GetImplicitAssetBundleVariantName(assetPath)
                    };
                }
            }
            
            // 包含元数据信息
            if (includeMetadata)
            {
                result["metadata"] = GetAssetMetadata(assetPath, mainAssetType);
            }
            
            // 包含导入设置
            if (includeImportSettings)
            {
                AssetImporter importer = AssetImporter.GetAtPath(assetPath);
                if (importer != null)
                {
                    result["importSettings"] = GetImportSettings(importer);
                }
            }
            
            Debug.Log($"成功获取资源信息: {assetPath}");
            
            return MCPResponse.Success(result);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"获取资源信息时出错: {e.Message}");
            return MCPResponse.Error($"获取资源信息失败: {e.Message}");
        }
    }
    
    /// <summary>
    /// 获取资源元数据
    /// </summary>
    private Dictionary<string, object> GetAssetMetadata(string assetPath, System.Type assetType)
    {
        var metadata = new Dictionary<string, object>();
        
        try
        {
            if (assetType == typeof(Texture2D))
            {
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                if (texture != null)
                {
                    metadata["width"] = texture.width;
                    metadata["height"] = texture.height;
                    metadata["format"] = texture.format.ToString();
                    metadata["mipmapCount"] = texture.mipmapCount;
                    metadata["filterMode"] = texture.filterMode.ToString();
                    metadata["wrapMode"] = texture.wrapMode.ToString();
                    metadata["anisoLevel"] = texture.anisoLevel;
                }
            }
            else if (assetType == typeof(AudioClip))
            {
                AudioClip audio = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
                if (audio != null)
                {
                    metadata["length"] = audio.length;
                    metadata["frequency"] = audio.frequency;
                    metadata["channels"] = audio.channels;
                    metadata["samples"] = audio.samples;
                    metadata["loadType"] = audio.loadType.ToString();
                }
            }
            else if (assetType == typeof(Mesh))
            {
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
                if (mesh != null)
                {
                    metadata["vertexCount"] = mesh.vertexCount;
                    metadata["triangleCount"] = mesh.triangles.Length / 3;
                    metadata["subMeshCount"] = mesh.subMeshCount;
                    metadata["bounds"] = new Dictionary<string, object>
                    {
                        ["center"] = new Dictionary<string, float>
                        {
                            ["x"] = mesh.bounds.center.x,
                            ["y"] = mesh.bounds.center.y,
                            ["z"] = mesh.bounds.center.z
                        },
                        ["size"] = new Dictionary<string, float>
                        {
                            ["x"] = mesh.bounds.size.x,
                            ["y"] = mesh.bounds.size.y,
                            ["z"] = mesh.bounds.size.z
                        }
                    };
                }
            }
            else if (assetType == typeof(Material))
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
                if (material != null)
                {
                    metadata["shader"] = material.shader != null ? material.shader.name : "Unknown";
                    metadata["renderQueue"] = material.renderQueue;
                    
                    // 获取材质属性
                    var properties = new List<Dictionary<string, object>>();
                    Shader shader = material.shader;
                    if (shader != null)
                    {
                        int propertyCount = ShaderUtil.GetPropertyCount(shader);
                        for (int i = 0; i < propertyCount; i++)
                        {
                            string propertyName = ShaderUtil.GetPropertyName(shader, i);
                            ShaderUtil.ShaderPropertyType propertyType = ShaderUtil.GetPropertyType(shader, i);
                            
                            properties.Add(new Dictionary<string, object>
                            {
                                ["name"] = propertyName,
                                ["type"] = propertyType.ToString(),
                                ["description"] = ShaderUtil.GetPropertyDescription(shader, i)
                            });
                        }
                    }
                    metadata["properties"] = properties;
                }
            }
            else if (assetType == typeof(AnimationClip))
            {
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
                if (clip != null)
                {
                    metadata["length"] = clip.length;
                    metadata["frameRate"] = clip.frameRate;
                    metadata["isLooping"] = clip.isLooping;
                    metadata["legacy"] = clip.legacy;
                    metadata["wrapMode"] = clip.wrapMode.ToString();
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"获取资源元数据失败 {assetPath}: {e.Message}");
        }
        
        return metadata;
    }
    
    /// <summary>
    /// 获取导入设置
    /// </summary>
    private Dictionary<string, object> GetImportSettings(AssetImporter importer)
    {
        var settings = new Dictionary<string, object>
        {
            ["type"] = importer.GetType().Name,
            ["assetBundleName"] = importer.assetBundleName,
            ["assetBundleVariant"] = importer.assetBundleVariant,
            ["userData"] = importer.userData
        };
        
        // 纹理导入设置
        if (importer is TextureImporter textureImporter)
        {
            settings["textureType"] = textureImporter.textureType.ToString();
            settings["alphaSource"] = textureImporter.alphaSource.ToString();
            settings["alphaIsTransparency"] = textureImporter.alphaIsTransparency;
            settings["sRGBTexture"] = textureImporter.sRGBTexture;
            settings["mipmapEnabled"] = textureImporter.mipmapEnabled;
            settings["maxTextureSize"] = textureImporter.maxTextureSize;
            settings["textureCompression"] = textureImporter.textureCompression.ToString();
        }
        // 音频导入设置
        else if (importer is AudioImporter audioImporter)
        {
            settings["loadInBackground"] = audioImporter.loadInBackground;
            settings["ambisonic"] = audioImporter.ambisonic;
            // preloadAudioData已弃用，使用defaultSampleSettings
            try
            {
                var defaultSettings = audioImporter.defaultSampleSettings;
                settings["loadType"] = defaultSettings.loadType.ToString();
                settings["compressionFormat"] = defaultSettings.compressionFormat.ToString();
            }
            catch
            {
                settings["loadType"] = "Unknown";
            }
        }
        // 模型导入设置
        else if (importer is ModelImporter modelImporter)
        {
            settings["globalScale"] = modelImporter.globalScale;
            settings["useFileScale"] = modelImporter.useFileScale;
            settings["importBlendShapes"] = modelImporter.importBlendShapes;
            settings["importVisibility"] = modelImporter.importVisibility;
            settings["importCameras"] = modelImporter.importCameras;
            settings["importLights"] = modelImporter.importLights;
            settings["animationType"] = modelImporter.animationType.ToString();
        }
        
        return settings;
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