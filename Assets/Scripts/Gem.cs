using UnityEngine;
using System.Collections;
using System.Collections.Generic;

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

            dragStart = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            isDragging = true;
        }

        private void OnMouseUp()
        {
            isDragging = false;
            
            // 檢查是否為特殊寶石且滑鼠點擊位置沒有明顯移動
            if (IsResourceGem &&
                Vector2.Distance(dragStart, Camera.main.ScreenToWorldPoint(Input.mousePosition)) < MIN_DRAG_DISTANCE)
            {
                // 使用 Board 實例中的 specialGemActivator
                if (Board.instance != null && Board.instance.specialGemActivator != null)
                {
                    Board.instance.specialGemActivator.啟動特殊寶石(this);
                }
            }

            GameObject.Find("/00GameMaster").GetComponent<GameManager>().updateRound();

        }
        private void OnMouseOver()
        {
            if (Input.GetMouseButtonDown(1)) 
            {
                List<Gem> desGem = new List<Gem>();
                desGem.Add(this);
                StartCoroutine(Board.instance.刪除寶石序列三(desGem));
            }                       
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

            Board.instance.嘗試交換寶石一(x, y, targetPos.x, targetPos.y);
        }

        private IEnumerator MoveAndActivate(Vector2Int targetPos)
        {
            var targetGem = Board.instance.GetGem(targetPos.x, targetPos.y);
            if (targetGem == null) yield break;

            // 先移動
            Board.instance.嘗試交換寶石一(x, y, targetPos.x, targetPos.y);
            yield return new WaitForSeconds(0.3f / Board.instance.gemMoveSpeed);

        }

        public IEnumerator AnimateMove(Vector3 target, float duration)
        {
            if (!IsValid()) yield break;

            isAnimating = true;
            var startPos = transform.position;
            float elapsed = 0;

            while (elapsed < duration && IsValid())
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                transform.position = Vector3.Lerp(startPos, target, t);
                yield return null;
            }

            if (IsValid())
            {
                transform.position = target;
                isAnimating = false;
            }
        }

        private bool IsValid()
        {
            return this != null &&
                   gameObject != null &&
                   Board.instance != null &&
                   Board.instance.gems[x, y] == this;
        }
    }
}