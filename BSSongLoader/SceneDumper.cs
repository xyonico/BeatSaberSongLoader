//
// History:
// version 1.0 - December 2010 - Yossarian King
 
using StreamWriter = System.IO.StreamWriter;

using UnityEngine;

namespace BSSongLoader
{
    public static class SceneDumper
    {
        public static void DumpScene()
        {
            string filename = Application.dataPath + "/unity-scene.txt";

            var gameObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();

            using (StreamWriter writer = new StreamWriter(filename, false))
            {
                foreach (GameObject gameObject in gameObjects)
                {
                    DumpGameObject(gameObject, writer, "");
                }
            }
        }

        private static void DumpGameObject(GameObject gameObject, StreamWriter writer, string indent)
        {
            writer.WriteLine("{0}+{1}", indent, gameObject.name);

            foreach (Component component in gameObject.GetComponents<Component>())
            {
                DumpComponent(component, writer, indent + "  ");
            }

            foreach (Transform child in gameObject.transform)
            {
                DumpGameObject(child.gameObject, writer, indent + "  ");
            }
        }

        private static void DumpComponent(Component component, StreamWriter writer, string indent)
        {
            writer.WriteLine("{0}{1}", indent, (component == null ? "(null)" : component.GetType().Name));
        }
    }
}