using System;
using System.IO;
using System.Text;
using JsonFx.U3DEditor;
using SharpZipLib.U3DEditor.GZip;
using UnityEditor;
using UnityEngine;

[JsonClassType]
[JsonOptIn]
public abstract class BaseGraph : ScriptableObject
{
    /// <summary>
    /// 保存在unity的asset中的序列化数据
    /// </summary>
    [HideInInspector]
    [JsonIgnore]
    [SerializeField]
    protected byte[] m_Data;

    /// <summary>
    /// 默认是懒惰模式，懒惰模式下如果数据有改变不会立即写入，避免贫频繁的数据写入保存
    /// </summary>
    [JsonIgnore]
    protected bool m_LazyMode = true;

    [JsonIgnore]
    protected bool m_IsDirty;

    /// <summary>
    /// 存储配置的文件路径
    /// </summary>
    [JsonIgnore]
    public string FilePath { get; set; }

    /// <summary>
    /// 存储配置的资源路径(默认文件路径优先级高，如果FilePath存在则先从FilePath中加载Graph的配置)
    /// </summary>
    [JsonIgnore]
    public string AssetPath { get; set; }

    protected BaseGraph()
    {

    }

    public virtual void SetGraphDirty()
    {
        if (!m_LazyMode)
        {
            m_IsDirty = false;
            Save();
        }
        else
        {
            m_IsDirty = true;
        }
    }

    public virtual void SaveGraph()
    {
        if (m_IsDirty || !m_LazyMode)
        {
            m_IsDirty = false;
            Save();
        }
    }

    protected virtual void Save()
    {
        if (!string.IsNullOrEmpty(FilePath))
        {
            var content = JsonWriter.Serialize(this, new JsonWriterSettings() { MaxDepth = Int32.MaxValue });
            var filePath = Path.GetFullPath(FilePath);
            var dirPath = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(dirPath))
            {
                Directory.CreateDirectory(dirPath);
            }

            if (!File.Exists(filePath))
            {
                File.Create(filePath).Close();
            }

            File.WriteAllBytes(filePath, Compress(content));
        }
        else if(!string.IsNullOrEmpty(AssetPath))
        { 
            var info = JsonWriter.Serialize(this, new JsonWriterSettings() { MaxDepth = Int32.MaxValue });
            m_Data = Compress(info);
            EditorUtility.SetDirty(this);
        }        
    }

    public static byte[] Compress(string content)
    {
        MemoryStream memoryStream = new MemoryStream();
        using (GZipOutputStream outStream = new GZipOutputStream(memoryStream))
        {
            var data = Encoding.UTF8.GetBytes(content);
            outStream.IsStreamOwner = false;
            outStream.SetLevel(4);
            outStream.Write(data, 0, data.Length);
            outStream.Flush();
            outStream.Finish();
        }
        return memoryStream.GetBuffer();
    }

    public static byte[] Decompress(byte[] bytesToDecompress)
    {
        byte[] writeData = new byte[4096];
        GZipInputStream s2 = new GZipInputStream(new MemoryStream(bytesToDecompress));
        MemoryStream outStream = new MemoryStream();
        while (true)
        {
            int size = s2.Read(writeData, 0, writeData.Length);
            if (size > 0)
            {
                outStream.Write(writeData, 0, size);
            }
            else
            {
                break;
            }
        }
        s2.Close();
        byte[] outArr = outStream.ToArray();
        outStream.Close();
        return outArr;
    }
}
