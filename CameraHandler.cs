using UnityEngine;

//Based on code from Sebastian Graves' excellent tutorial series, episodes 2 and 3:
//https://www.youtube.com/watch?v=c1FYp1oOFIs&list=PLD_vBJjpCwJtrHIW1SS5_BNRk6KZJZ7_d&index=3&pp=iAQB
//https://www.youtube.com/watch?v=Don3lGSAF2A&list=PLD_vBJjpCwJtrHIW1SS5_BNRk6KZJZ7_d&index=4&pp=iAQB

// I (BigMekBen) cleaned up the variable names to make it more clear what's going on.  
// For example, there are already industry standard names such as pan and tilt that can be used
//  instead of the borrowed terms "pivot", "target", etc.
   
// I'm sharing this here because I put time into understanding the code and thought it could be useful to others,
// and also wanted to show off my refactoring skills and legacy code maintenance skills.

// With kind regards to Sebastian Graves: thank you Sebastian for authoring the code in the original tutorial!

namespace SG
{
    public class CameraHandler : MonoBehaviour
    {
        // must drag in the Player object via the Unity Inspector
        public Transform targetTransform;  

        // must drag in the Main Camera object via the Unity Inspector
        public Transform cameraTransform; 

        // must drag in the Camera Mount object via the Unity Inspector (tutorial named it Camera Pivot)
        public Transform cameraMountTransform; // tutorial called this "cameraPivotTransform"
        
        private Transform myTransform;
        private Vector3 cameraTransformPosition;
        private LayerMask ignoreLayers;
        private Vector3 cameraFollowVelocity = Vector3.zero;

        // Ben added so we can quickly see the effect of the work in Episode 3 (with/without)
        // by toggling it in-game:
        public bool WallXRay = false;

        public static CameraHandler singleton;

        public float lookSpeed = 0.1f;
        public float followSpeed = 0.1f;
        public float pivotSpeed = 0.03f;

        private float backupDistance; // tutorial called this "targetPosition"
        public float backupDistanceNoObstacles;  // tutorial called this "defaultPosition"
        [SerializeField] // temp, to see it in inspector
        private float panDegrees; // previously: lookAngle
        [SerializeField] // temp, to see it in inspector
        private float tiltDegrees; // previously: pivotAngle
        
        public float minimumTilt = -35f; // previously: minimumPivot
        public float maximumTilt = 35f;  // previously: maximumPivot

        public float cameraSphereRadius = 0.2f;
        public float cameraCollisionMargin = 0.2f;  // previously: "cameraCollisionOffset"
        public float minimumCollisionOffset = 0.2f;

        private void Awake()
        {
            singleton = this;
            myTransform = transform;
            // Default it to the authored amount. Can be overridden via inspector:
            backupDistanceNoObstacles = cameraTransform.localPosition.z;
            ignoreLayers = ~(1 << 8 | 1 << 9 | 1 << 10);

        }

        // Makes camera handler object "on a tether" to the player.
        // The goal is:
        // - affect the position of the Camera Holder
        public void FollowTarget(float delta)
        {
            Vector3 targetPosition = Vector3.SmoothDamp(myTransform.position, targetTransform.position, 
                ref cameraFollowVelocity, delta/followSpeed);
            myTransform.position = targetPosition;
            HandleCameraCollisions(delta);
        }

        // The goal is:
        // - affect the rotation (pan, tilt) of the Camera Mount (previously Camera Pivot)
        public void HandleCameraRotation(float delta, float mouseXInput, float mouseYInput)
        {
            Vector3 rotation = Vector3.zero;
            panDegrees += (mouseXInput * lookSpeed) / delta;
            // Prevent pan amount going up forever as player pans around completely and repeatedly.
            // This is effectively cosmetic.
            // (if interested, see discussion at bottom of file ***)
            panDegrees %= 360f;

            rotation.y = panDegrees;
            Quaternion targetRotation = Quaternion.Euler(rotation);
            myTransform.rotation = targetRotation;

            rotation = Vector3.zero;
            // If player configuration option to invert the camera is added,
            // can use += instead, depending on player option:
            tiltDegrees -= (mouseYInput * pivotSpeed) / delta;
            tiltDegrees = Mathf.Clamp(tiltDegrees, minimumTilt, maximumTilt);
            rotation.x = tiltDegrees;

            targetRotation = Quaternion.Euler(rotation);
            cameraMountTransform.localRotation = targetRotation; 
        }

        // The goal is to ultimately affect only the Z position of the Main Camera
        // so that it stops just before clipping into a wall or other obstacle.
        public void HandleCameraCollisions(float delta)
        {
            // Feature added by Ben!
            if (WallXRay)
                return;

            backupDistance = backupDistanceNoObstacles;
            RaycastHit hit;
            // this was named "direction" in tutorial:
            Vector3 towardViewer = cameraTransform.position - cameraMountTransform.position;
            towardViewer.Normalize();

            if(Physics.SphereCast(
                origin:cameraMountTransform.position, 
                radius:cameraSphereRadius, 
                direction:towardViewer, 
                hitInfo:out hit, 
                maxDistance:Mathf.Abs(backupDistance), 
                layerMask:ignoreLayers))
            {
                // Assume camera Z is always negative*,
                // and we know that the magnitude of any vector is always positive:
                backupDistance = -(
                    (float)Vector3.Distance(cameraMountTransform.position, hit.point) 
                    - cameraCollisionMargin);
            }

            // this check prevents the backup distance from "springing" across 
            // the zero point in above calculation using the margin:
            if (Mathf.Abs(backupDistance) < minimumCollisionOffset)
            {
                // Assume camera Z is always negative*
                backupDistance = -minimumCollisionOffset;
            }

            // * Assumption is valid because of the handedness of our coordinate system.
            //      Furthermore, the Z of the camera should be set to negative value in Inspector
            //      in order for player to be visible.

            // "Back the camera up" by the needed amount with linear smoothing:
            cameraTransformPosition.z = Mathf.Lerp(cameraTransform.localPosition.z, backupDistance, delta / 0.2f);
            cameraTransform.localPosition = cameraTransformPosition;
        }
    }
}
// *** About the overflow of the pan angle:
//      Admittedly, it gives the same result in most cases.  Here is a discussion of the 
//  points on either side:

// Arguments for using the modulo:
// - It's cosmetic for the designer/engineer; the angle increasing past 360 looks 
// obnoxious in the Inspector.
// - If the overflow ever occurred, it would give a brief jarring effect as the overflow value
// would temporarily cause the angle to "jump" since it wouldn't be a multiple of 360 when
// the overflow occurred.
// - An automated test could conceivably spin the camera 10^38 times, triggering the overflow.

// Arguments for skipping the modulo:
// - The player won't see the values in the Inspector; only the designer/engineer would.
// - A human being would never have time to spin the camera around that many times.
// - We can set up various experiments to see how Unity would handle the situation, but since 
// the experiments would be contrived, they might not prove anything for when the "real" issue occurred.
//      -- For example, a quick experiment could set the panDegrees to just under the positive largest value
//          in a C# float.  I tried the following expressions in C# interactive mode:
//          float.MaxValue % 360.0f
//          (float.MaxValue - 1f) % 360f
//          ...and they both evaluated to 0.  This could be a rounding error in C# interactive mode,
//          so I can't establish from this how Unity would handle values close to float.MaxValue.
//      -- You could set the rotation speed very high to get to the overflow faster; but at that point,
//      it would be impossible to see the impact visually, anyway.
// - A human isn't likely to be able to observe the visual effect even if an automated test triggered
// the overflow.
// - We shouldn't run an extra CPU operation in 10^38 cases just to handle the one time it could occur.

// So, rationally speaking, I think that the "skip the modulo" argument wins.
// But for learning purposes, I think the COSMETIC effect of clamping the angle in the Inspector
// to +-360.0f is preferable.
// In my experience as a software engineer, business users will sometimes demand cosmetic changes that do
// not impact functionality or reliability of the system; the psychological effect is valued more than
// reason.  For example, it could erode the user's attention as they "waste" brain activity telling 
// themselves that 420 degrees is the same angle as 60 degrees.
// I imagine player audiences or DCC users would have similar demands at times.