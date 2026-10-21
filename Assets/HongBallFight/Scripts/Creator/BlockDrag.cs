using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HongBallFight
{
    public sealed class BlockDragData
    {
        public SkillBlock Block;
        public Func<SkillBlock> NewBlock;
        public Func<BlockValue> NewValue;
        public string Title;
    }
    public sealed class BlockDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public Func<BlockDragData> Data;
        public BallFightApp App;
        public RectTransform Overlay;
        public static BlockDragData Active;
        static RectTransform ghost;
        public void OnBeginDrag(PointerEventData eventData)
        {
            Cancel(); Active = Data();
            ghost = App.Box(Overlay, "Dragging block", new Rect(0, 0, 250, 42), new Color32(77, 132, 220, 220));
            App.Label(ghost, Active.Title, new Rect(10, 0, 230, 42), 16);
            OnDrag(eventData);
        }
        public void OnDrag(PointerEventData eventData)
        {
            if (!ghost) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Overlay, eventData.position, eventData.pressEventCamera, out var point);
            ghost.position = Overlay.TransformPoint(point + new Vector2(12, -12));
        }
        public void OnEndDrag(PointerEventData eventData) { Cancel(); }
        public static void Cancel() { Active = null; if (ghost) Destroy(ghost.gameObject); ghost = null; }
    }
    public sealed class BlockDropZone : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public Action<BlockDragData> Drop;
        Image image;
        Color original;
        void Awake() { image = GetComponent<Image>(); if (image) { original = image.color; image.raycastTarget = true; } }
        public void OnDrop(PointerEventData eventData)
        {
            var data = BlockDrag.Active;
            if (data == null) return;
            BlockDrag.Cancel(); Drop?.Invoke(data);
        }
        public void OnPointerEnter(PointerEventData eventData) { if (BlockDrag.Active != null && image) image.color = new Color(.25f, .8f, .65f, .8f); }
        public void OnPointerExit(PointerEventData eventData) { if (image) image.color = original; }
    }
}
