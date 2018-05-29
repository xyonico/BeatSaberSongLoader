using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace SongLoaderPlugin.Parallel {

    public class ThreadHandler : MonoBehaviour {

        private Queue<ThreadStart> tasks = new Queue<ThreadStart>();

        /// <summary>
        /// Executes the provided task within the .Net managed thread pool
        /// DO NOT USE THE UNITY API FROM EXTERNAL THREADS
        /// </summary>
        /// <param name="task"></param>
        public void Dispatch(ThreadStart task) {
            ThreadPool.QueueUserWorkItem(o => task());
        }

        /// <summary>
        /// Adds the provided task to the task queue, pending execution on the main update thread
        /// </summary>
        /// <param name="task"></param>
        public void Post(ThreadStart task) {
            tasks.Enqueue(task);
        }

        private void LateUpdate() {
            if (tasks.IsEmpty())
                return;
            ThreadStart task = tasks.Dequeue();
            task?.Invoke();
        }
    }

    public static class ICollectionExtension {

        public static bool IsEmpty(this ICollection queue) {
            return queue.Count == 0;
        }

    }

}