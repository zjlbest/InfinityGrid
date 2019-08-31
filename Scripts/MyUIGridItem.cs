using UnityEngine;

namespace MyUIComponent
{
    public class MyUIGridItem : MonoBehaviour
    {
        /// <summary>
        /// 索引
        /// </summary>
        [HideInInspector]
        public int index;

        public virtual void SetData(object data)
        {
            
        }

        public virtual void OnSpawn()
        {
            gameObject.SetActive(true);
        }

        public virtual void OnUnSpawn()
        {
            gameObject.SetActive(false);
        }
    }
}