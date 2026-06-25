using UnityEngine;
using UnityEngine.Animations;

namespace ThoughtBubble
{
    /// <summary>
    /// Binds a ParentConstraint's source to an OVRSkeleton wrist bone at runtime.
    ///
    /// OVRSkeleton instantiates its bone transforms at runtime (the Bones list is
    /// empty in the Editor), so the wrist bone can't be wired into the constraint
    /// from the Inspector. This resolves it once the skeleton is initialized.
    ///
    /// Put this + a ParentConstraint on your OWN button object, anywhere in the
    /// hierarchy. The object never becomes a child of the Meta hand; the only Meta
    /// reference is the OVRSkeleton dragged into _skeleton.
    /// </summary>
    [RequireComponent(typeof(ParentConstraint))]
    public class BindToWristBone : MonoBehaviour
    {
        [Tooltip("The hand's OVRSkeleton (e.g. on '[BuildingBlock] Hand Tracking left').")]
        [SerializeField] private OVRSkeleton _skeleton;

        [Tooltip("Which bone to follow. HandWristRoot sits where a watch would.")]
        [SerializeField] private OVRSkeleton.BoneId _boneId = OVRSkeleton.BoneId.Hand_WristRoot;

        private ParentConstraint _constraint;

        private void Awake()
        {
            _constraint = GetComponent<ParentConstraint>();
        }

        private void Update()
        {
            // Already bound; nothing left to do.
            if (_constraint.sourceCount > 0)
            {
                enabled = false;
                return;
            }

            if (_skeleton == null || !_skeleton.IsInitialized)
                return;

            foreach (var bone in _skeleton.Bones)
            {
                if (bone.Id != _boneId)
                    continue;

                _constraint.AddSource(new ConstraintSource
                {
                    sourceTransform = bone.Transform,
                    weight = 1f
                });
                _constraint.constraintActive = true;

                // Tune the button's resting spot on the wrist via the constraint's
                // Translation/Rotation Offset fields in the Inspector.
                enabled = false;
                return;
            }

            Debug.LogWarning($"{nameof(BindToWristBone)}: bone '{_boneId}' not found on skeleton '{_skeleton.name}'.", this);
        }
    }
}
