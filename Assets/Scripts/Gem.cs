using UnityEngine;
using System.Collections;

namespace Match3Game
{
    public class Gem : MonoBehaviour
    {
        public int id;
        public int x, y;
        public bool isMatched = false;
        public bool isAnimating = false;

        private Vector2 dragStart;
        private bool isDragging = false;
        private const float DRAG_THRESHOLD = 0.3f;
        private const float MIN_DRAG_DISTANCE = 0.1f;

        public void Init(int gemId, int posX, int posY)
        {
            id = gemId;
            x = posX;
            y = posY;
            transform.position = new Vector3(posX, posY, 0);
        }

        private void Update()
        {
            if (!isDragging || isAnimating) return;

            Vector2 currentPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2 dragDelta = currentPos - dragStart;

            if (dragDelta.magnitude < MIN_DRAG_DISTANCE) return;

            bool isHorizontal = Mathf.Abs(dragDelta.x) > Mathf.Abs(dragDelta.y);
            Vector2Int targetPos = new Vector2Int(
                x + (isHorizontal ? (dragDelta.x > 0 ? 1 : -1) : 0),
                y + (!isHorizontal ? (dragDelta.y > 0 ? 1 : -1) : 0)
            );

            if (dragDelta.magnitude >= DRAG_THRESHOLD)
            {
                isDragging = false;
                TrySwap(targetPos);
            }
        }

        private void OnMouseDown()
        {
            if (isAnimating) return;
            dragStart = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            isDragging = true;
        }

        private void OnMouseUp()
        {
            isDragging = false;
        }

        private void TrySwap(Vector2Int targetPos)
        {
            if (targetPos.x < 0 || targetPos.x >= Board.instance.width ||
                targetPos.y < 0 || targetPos.y >= Board.instance.height)
                return;

            isAnimating = true;
            Board.instance.TrySwapGems(x, y, targetPos.x, targetPos.y);
        }

        public IEnumerator AnimateMove(Vector3 target, float duration)
        {
            Vector3 start = transform.position;
            float elapsed = 0;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                transform.position = Vector3.Lerp(start, target, t);
                yield return null;
            }

            transform.position = target;
            isAnimating = false;
        }
    }
}