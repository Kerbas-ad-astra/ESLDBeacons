using System;
using System.Collections.Generic;
using System.Linq;

namespace ESLDCore
{
    public static class SerializationHelper
    {
        public static List<T> LoadObjects<T>(this PartModule partModule, string nodeName, ConfigNode saveNode, int moduleIndex, string identifierKey = "name", bool removeUnsaved = false) where T : class, IConfigNode, new()
        {
            return LoadObjects<T>(nodeName, saveNode, partModule.part.partInfo.partConfig.GetNodes("MODULE", "name", partModule.GetType().Name)[moduleIndex], identifierKey, removeUnsaved);
        }
        public static List<T> LoadObjects<T>(this PartModule partModule, string nodeName, ConfigNode saveNode, string identifierKey = "name", bool removeUnsaved = false) where T : class, IConfigNode, new()
        {
            return LoadObjects<T>(nodeName, saveNode, partModule.part.partInfo.partConfig.GetNodes("MODULE", "name", partModule.GetType().Name)[0], identifierKey, removeUnsaved);
        }

        public static List<T> LoadObjects<T>(string nodeName, ConfigNode saveNode, ConfigNode cfgNode, string identifierKey = "name", bool removeUnsaved = false) where T : class, IConfigNode, new()
        {
            List<T> objects = new List<T>();
            if (!saveNode.HasNode(nodeName) && !cfgNode.HasNode(nodeName))
                return objects;

            ConfigNode[] nodes = cfgNode.GetNodes(nodeName);
            ConfigNode[] savedNodes = saveNode.GetNodes(nodeName);

            for (int i = 0; i < nodes.Length; i++)
            {
                ConfigNode savedNode = savedNodes.FirstOrDefault(n => n.GetValue(identifierKey) == nodes[i].GetValue(identifierKey));
                if (removeUnsaved && savedNode == null)
                    continue;
                T obj = new T();
                obj.Load(nodes[i]);
                if (savedNode != null)
                    obj.Load(savedNode);
                objects.Add(obj);
            }
            for (int i = 0; i < savedNodes.Length; i++)
            {
                if (nodes.Any(n => n.GetValue(identifierKey) == savedNodes[i].GetValue(identifierKey)))
                    continue;
                T obj = new T();
                obj.Load(savedNodes[i]);
                objects.Add(obj);
            }

            return objects;
        }

        public static void LoadObjects<T>(List<T> objects, string nodeName, ConfigNode node, Func<T, string> identifierSelector, string identifierKey = "name") where T : class, IConfigNode, new()
        {
            ConfigNode[] nodes = node.GetNodes(nodeName);
            for (int i = 0; i < nodes.Length; i++)
            {
                int existingItem = objects.FindIndex(o => identifierSelector(o) == nodes[i].GetValue(identifierKey));
                if (existingItem < 0)
                {
                    T newItem = new T();
                    objects.Add(newItem);
                    newItem.Load(nodes[i]);
                }
                else
                    objects[existingItem].Load(nodes[i]);
            }
        }
    }
}
