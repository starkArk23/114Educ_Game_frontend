using System;
using UnityEngine;

[CreateAssetMenu(menuName = "NPC/Ambient NPC Visual Profile", fileName = "AmbientNpcVisualProfile")]
public class AmbientNpcVisualProfile : ScriptableObject
{
    [Serializable]
    public struct DirectionalSpriteAnimation
    {
        public Sprite idle;
        public Sprite[] walkFrames;

        public bool HasAnySprite
        {
            get
            {
                if (idle != null)
                    return true;

                if (walkFrames == null)
                    return false;

                for (int index = 0; index < walkFrames.Length; index++)
                {
                    if (walkFrames[index] != null)
                        return true;
                }

                return false;
            }
        }

        public Sprite Evaluate(bool isMoving, float cycleTime)
        {
            if (!isMoving)
                return idle != null ? idle : GetFirstWalkFrame();

            if (walkFrames != null && walkFrames.Length > 0)
            {
                int frameIndex = Mathf.FloorToInt(Mathf.Max(0f, cycleTime)) % walkFrames.Length;
                Sprite walkSprite = walkFrames[frameIndex];
                if (walkSprite != null)
                    return walkSprite;
            }

            return idle != null ? idle : GetFirstWalkFrame();
        }

        private Sprite GetFirstWalkFrame()
        {
            if (walkFrames == null)
                return null;

            for (int index = 0; index < walkFrames.Length; index++)
            {
                if (walkFrames[index] != null)
                    return walkFrames[index];
            }

            return null;
        }
    }

    public DirectionalSpriteAnimation down;
    public DirectionalSpriteAnimation up;
    public DirectionalSpriteAnimation left;
    public DirectionalSpriteAnimation right;

    public bool IsConfigured => down.HasAnySprite || up.HasAnySprite || left.HasAnySprite || right.HasAnySprite;

    public Sprite Evaluate(Vector2 facingDirection, bool isMoving, float cycleTime)
    {
        DirectionalSpriteAnimation direction = SelectDirection(facingDirection);
        return direction.Evaluate(isMoving, cycleTime);
    }

    private DirectionalSpriteAnimation SelectDirection(Vector2 facingDirection)
    {
        if (Mathf.Abs(facingDirection.x) >= Mathf.Abs(facingDirection.y))
            return facingDirection.x < 0f ? left : right;

        return facingDirection.y > 0f ? up : down;
    }
}