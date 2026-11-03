using UnityEngine;

namespace ElfAndSafety
{
    /// <summary>
    /// What a faulty part does once it's installed in a toy. Runs only where gameplay is decided (solo or the
    /// co-op host); the results (a toy moving, being dropped, a part popping off) sync like any other change.
    /// </summary>
    [RequireComponent(typeof(ToyPart))]
    public class PartMalfunction : MonoBehaviour
    {
        [Header("Runs away (overwound key, wobbly wheels)")]
        [Tooltip("How long the toy sits still before it sets off.")]
        [SerializeField] float runAwayDelay = 3f;
        [SerializeField] float runDuration = 2.5f;
        [SerializeField] float runSpeed = 0.9f;

        [Header("Zaps (leaky battery)")]
        [Tooltip("How long you can hold the toy before it shocks you into dropping it.")]
        [SerializeField] float zapDelay = 0.6f;

        [Header("Pops off (loose button eyes)")]
        [SerializeField] float popOffDelay = 2f;

        ToyPart part;
        float timer;
        float runTimeLeft;
        Vector3 runDirection;
        bool announced;

        /// <summary>True while the toy is running away under its own power.</summary>
        public bool IsRunning => runTimeLeft > 0f;

        void Awake()
        {
            part = GetComponent<ToyPart>();
        }

        void Update()
        {
            ToyBody toy = part.InstalledIn;
            if (toy == null || NetworkSession.IsRemoteClient)
            {
                timer = 0f;
                runTimeLeft = 0f;
                return;
            }

            switch (part.Fault)
            {
                case PartFault.Overwound:
                case PartFault.Wobbly:
                    UpdateRunAway(toy);
                    break;
                case PartFault.Leaky:
                    UpdateZap(toy);
                    break;
                case PartFault.LooseThreads:
                    UpdatePopOff(toy);
                    break;
            }
        }

        void UpdateRunAway(ToyBody toy)
        {
            Carryable body = toy.Carryable;
            if (body.State != CarryState.Loose)
            {
                timer = 0f;
                runTimeLeft = 0f;
                return;
            }

            if (runTimeLeft > 0f)
            {
                runTimeLeft -= Time.deltaTime;
                body.Nudge(runDirection * runSpeed);
                return;
            }

            if (!body.IsResting)
            {
                timer = 0f;
                return;
            }

            timer += Time.deltaTime;
            if (timer < runAwayDelay)
            {
                return;
            }

            timer = 0f;
            runTimeLeft = runDuration;
            runDirection = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;

            if (!announced)
            {
                announced = true;
                string what = part.Fault == PartFault.Wobbly ? "is driving off" : "is running away";
                WorkshopFeedback.Problem($"{toy.DisplayName} {what}! ({ToyPart.TellWord(part.Fault)} {ToyRecipe.PartName(part.Kind)})");
            }
        }

        void UpdateZap(ToyBody toy)
        {
            PlayerInteractor holder = toy.Carryable.Holder;
            if (holder == null)
            {
                timer = 0f;
                return;
            }

            timer += Time.deltaTime;
            if (timer < zapDelay)
            {
                return;
            }

            timer = 0f;
            holder.RequestDrop();
            WorkshopFeedback.Problem($"ZAP! {toy.DisplayName} shocked you ({ToyPart.TellWord(part.Fault)} battery)");
        }

        void UpdatePopOff(ToyBody toy)
        {
            if (!toy.Carryable.IsHeld)
            {
                timer = 0f;
                return;
            }

            timer += Time.deltaTime;
            if (timer < popOffDelay)
            {
                return;
            }

            timer = 0f;
            Transform toyTransform = toy.transform;
            Vector3 position = toyTransform.position + Vector3.up * 0.7f + toyTransform.forward * 0.2f;
            part.Carryable.SetLoose(position, toyTransform.rotation, (toyTransform.forward + Vector3.up) * 1.5f);
            WorkshopFeedback.Problem($"{part.DisplayName} fell off {toy.DisplayName}! (loose threads)");
        }
    }
}
