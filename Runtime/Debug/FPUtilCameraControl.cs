namespace FuzzPhyte.Utility.TestingDebug
{
    using UnityEngine;

    public class FPUtilCameraControl:MonoBehaviour
    {
        [Header("Should be the parent item we are controlling")]
        public Transform LocalTransform;
        public float movementSpeed = 10f; // Speed for moving forward/backward
        public float rotationSpeed = 100f; // Speed for rotating the camera
        public float dragSpeed = 0.1f; // Sensitivity for drag rotation

        private bool isTouching = false; // Is the player holding the screen?
        private Vector2 initialTouchPos;
        
        public Camera mainCamera;
        [SerializeField] private bool setup;
        [SerializeField] private bool useTouch;
        public virtual void Setup(Camera userSpecifiedCamera, bool client)
        {
            if (!client)
            {
                Destroy(this);
                return;
            }
            mainCamera = userSpecifiedCamera;
            mainCamera.enabled = true;
            if (Application.platform == RuntimePlatform.Android || Application.platform == RuntimePlatform.IPhonePlayer)
            {
                useTouch = true;
            }
            else
            {
                useTouch = false;
            }

            setup = true;
        }
        

        // Update is called once per frame
        public virtual void Update()
        {
            if (!setup)
            {
                return;
            }
            // Platform specific controls
            if (useTouch)
            {
                HandleTouchInput();
            }
            else
            {
                HandleMouseKeyboardInput();
            }
        }

        // Handles touch input for mobile devices
        public virtual void HandleTouchInput()
        {
            if (Input.touchCount > 0)
            {
                Debug.Log($"Touch Detected!");
                Touch touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began)
                {
                    isTouching = true;
                    initialTouchPos = touch.position;
                }

                if (touch.phase == TouchPhase.Moved && isTouching)
                {
                    Vector2 delta = touch.position - initialTouchPos;
                    RotateCamera(0, delta.y);
                    initialTouchPos = touch.position;
                }
                if (Input.touchCount == 2)
                {
                    if (touch.phase == TouchPhase.Stationary && isTouching)
                    {
                        // Move camera forward in the direction it's facing
                        MoveCamera(Vector3.forward);
                    }
                }
                if(Input.touchCount == 3)
                {
                    if (touch.phase == TouchPhase.Stationary && isTouching)
                    {
                        // Move camera forward in the direction it's facing
                        MoveCamera(Vector3.back);
                    }
                }
                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    isTouching = false;
                }
                
            }
        }

        // Handles keyboard and mouse input for desktop
        public virtual void HandleMouseKeyboardInput()
        {
            // Keyboard movement (WASD or Arrow keys)
            Vector3 move = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
            MoveCamera(move);
            Debug.Log("Move: " + move);

            // Mouse rotation
            if (Input.GetMouseButton(1)) // Right-click to rotate
            {
                float mouseX = Input.GetAxis("Mouse X");
                float mouseY = Input.GetAxis("Mouse Y");
                RotateCamera(mouseX, mouseY);
            }
        }

        // Moves the camera in a specific direction
        public virtual void MoveCamera(Vector3 direction)
        {
            LocalTransform.Translate(direction * movementSpeed * Time.deltaTime);
            
        }

        // Rotates the camera based on input
        public virtual void RotateCamera(float deltaX, float deltaY)
        {
            float rotationX = deltaX * rotationSpeed * Time.deltaTime;
            float rotationY = -deltaY * rotationSpeed * Time.deltaTime;

            LocalTransform.Rotate(0, rotationX, 0); // Horizontal rotation (Y axis)
            LocalTransform.Rotate(rotationY, 0, 0); // Vertical rotation (X axis)
        }

        // Checks if the touch/mouse position is within the defined RectTransform
        /*
        bool IsTouchWithinRect(Vector2 screenPosition)
        {
            Vector2 localPoint;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(touchRegionRect, screenPosition, mainCamera, out localPoint))
            {
                // Check if the local point is inside the rectangle
                return touchRegionRect.rect.Contains(localPoint);
            }

            return false;
        }
        */

        public virtual void LateUpdate()
        {
            
        }
    }
}
