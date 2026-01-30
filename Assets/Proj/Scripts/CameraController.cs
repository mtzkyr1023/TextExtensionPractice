using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace EasyAsset
{
    public class CameraController : MonoBehaviour
    {
        [SerializeField] private Vector2 rotationSpeed;
        [SerializeField] private float attenuation;
        [SerializeField] private float moveSpeed;

        private Vector3 lastMousePosition;

        private float rotationX = 0.0f;
        private float rotationY = 0.0f;

        private Vector3 movement = Vector3.zero;

        private float waitTimer = 0.0f;
        
        void Start()
        {
            rotationX = transform.localRotation.eulerAngles.x;
            rotationY = transform.localRotation.eulerAngles.y;
        }

        void Update()
        {
            Vector3 diff = Vector3.zero;

            if (Input.GetKey(KeyCode.W))
            {
                movement += transform.forward * moveSpeed * Time.deltaTime;
            }
            if (Input.GetKey(KeyCode.S))
            {
                movement -= transform.forward * moveSpeed * Time.deltaTime;
            }
            if (Input.GetKey(KeyCode.A))
            {
                movement -= transform.right * moveSpeed * Time.deltaTime;
            }
            if (Input.GetKey(KeyCode.D))
            {
                movement += transform.right * moveSpeed * Time.deltaTime;
            }
            if (Input.GetKey(KeyCode.E))
            {
                movement += transform.up * moveSpeed * Time.deltaTime;
            }
            if (Input.GetKey(KeyCode.Q))
            {
                movement -= transform.up * moveSpeed * Time.deltaTime;
            }

            if (Input.GetMouseButtonDown(1))
            {
                lastMousePosition = Input.mousePosition;
            }
            if (Input.GetMouseButton(1))
            {
                diff = (lastMousePosition - Input.mousePosition);


                waitTimer = 1.0f;
            }

            waitTimer -= Time.deltaTime;

            rotationX -= diff.x * rotationSpeed.x * Time.deltaTime;
            rotationY -= diff.y * rotationSpeed.y * Time.deltaTime;

            //rotationX = Mathf.Clamp(rotationX, -1.0f, 1.0f);
            //rotationY = Mathf.Clamp(rotationY, -1.0f, 1.0f);

            transform.localRotation =
                Quaternion.AngleAxis(rotationX, Vector3.up) *
                Quaternion.AngleAxis(rotationY, Vector3.left);

            transform.localPosition += movement;

            movement = Vector3.zero;

            //rotationX = Mathf.Lerp(rotationX, 0.0f, attenuation * Time.deltaTime);
            //rotationY = Mathf.Lerp(rotationY, 0.0f, attenuation * Time.deltaTime);


            lastMousePosition = Input.mousePosition;
        }
    }
}