using System.Collections.Generic;
using UnityEngine;

namespace DodgeBallSim.AI
{
    public struct SensorDetection
    {
        public GameObject gameObject;
        public Vector3 relativePosition; 
        public float distance;
        public string tag;
    }

    public class SensorSystem : MonoBehaviour
    {
        [Header("視界設定")]
        public float viewDistance = 15f;    
        [Range(0f, 180f)] 
        public float viewAngle = 120f;    
        public int rayCount = 15;         

        [Header("検知レイヤー")]
        [SerializeField] private LayerMask detectionMask = ~0; 

        public List<SensorDetection> ScanEnvironment()
        {
            List<SensorDetection> detections = new List<SensorDetection>();
            float halfAngle = viewAngle / 2f;
            float angleStep = viewAngle / (rayCount - 1);

            for (int i = 0; i < rayCount; i++)
            {
                float currentAngle = -halfAngle + (angleStep * i);
                Vector3 dir = Quaternion.Euler(0, currentAngle, 0) * transform.forward;

                Vector3 rayOrigin = new Vector3(transform.position.x, 0.5f, transform.position.z) + dir * 0.6f;

                RaycastHit hit;
                // ズラした分、射程距離を少し調整
                float adjustedDistance = viewDistance - 0.6f; 
                bool isHit = Physics.Raycast(rayOrigin, dir, out hit, adjustedDistance, detectionMask);

                if (isHit)
                {
                    if (!hit.collider.CompareTag("Ground") && !hit.collider.gameObject.name.Contains("Wall"))
                    {
                        SensorDetection detection = new SensorDetection
                        {
                            gameObject = hit.collider.gameObject,
                            relativePosition = transform.InverseTransformPoint(hit.point),
                            distance = hit.distance,
                            tag = hit.collider.tag
                        };
                        detections.Add(detection);
                    }
                }

                // デバッグ用
                Color rayColor = isHit ? Color.red : Color.green;
                Debug.DrawRay(rayOrigin, dir * (isHit ? hit.distance : adjustedDistance), rayColor);
            }

            return detections;
        }
    }
}