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

        private bool IsResourceGem => id >= 100;

        public void Init(int gemId, int posX, int posY)
        {
            id = gemId;
            x = posX;
            y = posY;
            transform.position = new Vector3(posX, posY, 0);
        }

        private void Update()
        {
            if (!isDragging ||
                isAnimating ||
                !Board.instance.hasMoveCompleted ||
                Board.instance == null)
            {
                return;
            }

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
            if (isAnimating || !Board.instance.hasMoveCompleted ||
        Board.instance == null) return;

            // 點擊資源寶石時直接觸發效果
            //if (IsResourceGem && Input.GetMouseButtonDown(0))
            //{
            //    Board.instance.ActivateResourceGem(this);
            //    return;
            //}

            dragStart = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            isDragging = true;
        }

        private void OnMouseUp()
        {
            isDragging = false;
        }

        private void TrySwap(Vector2Int targetPos)
        {
            // 檢查目標位置是否有效
            if (targetPos.x < 0 || targetPos.x >= Board.instance.width ||
                targetPos.y < 0 || targetPos.y >= Board.instance.height)
            {
                isAnimating = false;
                return;
            }

            // 檢查是否可以進行交換
            var targetGem = Board.instance.GetGem(targetPos.x, targetPos.y);
            if (targetGem == null || targetGem.isAnimating)
            {
                isAnimating = false;
                return;
            }

            // 如果是資源寶石，確保可以移動
            if (IsResourceGem)
            {
                StartCoroutine(MoveAndActivate(targetPos));
                return;
            }

            Board.instance.TrySwapGems(x, y, targetPos.x, targetPos.y);
        }
        private IEnumerator MoveAndActivate(Vector2Int targetPos)
        {
            var targetGem = Board.instance.GetGem(targetPos.x, targetPos.y);
            if (targetGem == null) yield break;

            // 先移動
            Board.instance.TrySwapGems(x, y, targetPos.x, targetPos.y);
            yield return new WaitForSeconds(0.3f / Board.instance.gemMoveSpeed);

            // 移動完後觸發效果
            Board.instance.specialGemActivator.ActivateSpecialGem(this);
        }
        public IEnumerator AnimateMove(Vector3 target, float duration)
        {
            if (duration <= 0)
            {
                transform.position = target;
                isAnimating = false;
                yield break;
            }

            Vector3 start = transform.position;
            float elapsed = 0;

            isAnimating = true;
            while (elapsed < duration)
            {
                if (this == null || gameObject == null)
                {
                    yield break;
                }

                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                transform.position = Vector3.Lerp(start, target, t);
                yield return null;
            }

            if (this != null && gameObject != null)
            {
                transform.position = target;
            }
            isAnimating = false;
        }

    }
}