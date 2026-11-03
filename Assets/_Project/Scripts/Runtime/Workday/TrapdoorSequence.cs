using System.Collections;
using UnityEngine;

namespace ElfAndSafety
{
    /// <summary>
    /// Santa's disciplinary process, in placeholder form: pull the player over the trapdoor, swing the doors
    /// open over an orange glow, and drop the camera into the pit. Purely presentational: the verdict has
    /// already been decided when this plays.
    /// </summary>
    public class TrapdoorSequence : MonoBehaviour
    {
        [Tooltip("Hinge transforms; each door swings down around its hinge's local Z axis.")]
        [SerializeField] Transform leftHinge;
        [SerializeField] Transform rightHinge;
        [SerializeField] Light furnaceLight;
        [Tooltip("Where the player stands before the fall, facing Santa's window.")]
        [SerializeField] Transform standPoint;

        [SerializeField] float gatherDuration = 0.7f;
        [SerializeField] float openDuration = 0.5f;
        [SerializeField] float fallDuration = 1.3f;
        [SerializeField] float fallDepth = 5f;
        [SerializeField] float furnaceIntensity = 12f;

        public bool HasPlayed { get; private set; }

        void Awake()
        {
            if (furnaceLight != null) furnaceLight.intensity = 0f;
        }

        public IEnumerator Play(Transform player)
        {
            HasPlayed = true;

            // Take the player out of normal movement for the cutscene.
            var controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            var movement = player.GetComponent<FirstPersonController>();
            Transform cameraPivot = movement != null ? movement.CameraPivot : null;

            Vector3 startPosition = player.position;
            Quaternion startRotation = player.rotation;
            Quaternion startPitch = cameraPivot != null ? cameraPivot.localRotation : Quaternion.identity;
            Vector3 stand = standPoint != null ? standPoint.position : transform.position;
            Quaternion standRotation = standPoint != null ? standPoint.rotation : player.rotation;

            // 1. Gather: slide over the trapdoor and face Santa's office.
            yield return Animate(gatherDuration, t =>
            {
                float s = Mathf.SmoothStep(0f, 1f, t);
                player.SetPositionAndRotation(Vector3.Lerp(startPosition, stand, s), Quaternion.Slerp(startRotation, standRotation, s));
                if (cameraPivot != null) cameraPivot.localRotation = Quaternion.Slerp(startPitch, Quaternion.identity, s);
            });

            // 2. Doors swing open and the furnace glow comes up.
            yield return Animate(openDuration, t =>
            {
                float s = t * t;
                if (leftHinge != null) leftHinge.localRotation = Quaternion.Euler(0f, 0f, -95f * s);
                if (rightHinge != null) rightHinge.localRotation = Quaternion.Euler(0f, 0f, 95f * s);
                if (furnaceLight != null) furnaceLight.intensity = furnaceIntensity * t;
            });

            // 3. Fall, looking back up at the shrinking hole.
            yield return Animate(fallDuration, t =>
            {
                player.position = stand + Vector3.down * fallDepth * t * t;
                if (cameraPivot != null) cameraPivot.localRotation = Quaternion.Euler(-70f * Mathf.SmoothStep(0f, 1f, t), 0f, 25f * t);
                if (furnaceLight != null) furnaceLight.intensity = furnaceIntensity * (1f + t);
            });
        }

        static IEnumerator Animate(float duration, System.Action<float> step)
        {
            for (float time = 0f; time < duration; time += Time.deltaTime)
            {
                step(Mathf.Clamp01(time / duration));
                yield return null;
            }
            step(1f);
        }
    }
}
