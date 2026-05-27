using UnityEngine;

/// <summary>
/// 인벤토리에 배치된 아이템 인스턴스.
/// 그리드 내 위치, 회전 여부, 수량 등 런타임 상태를 관리합니다.
/// </summary>
[System.Serializable]
public class InventoryItem
{
    public ItemData Data { get; private set; }

    /// <summary> 그리드 내 좌상단 좌표 </summary>
    public Vector2Int GridPosition { get; set; }

    /// <summary> 90도 회전 여부 (width ↔ height 교환) </summary>
    public bool IsRotated { get; private set; }

    /// <summary> 현재 스택 수량 </summary>
    public int StackCount { get; private set; }

    /// <summary> 현재 상태의 가로 칸 수 </summary>
    public int Width => IsRotated ? Data.height : Data.width;

    /// <summary> 현재 상태의 세로 칸 수 </summary>
    public int Height => IsRotated ? Data.width : Data.height;

    public InventoryItem(ItemData data, Vector2Int gridPos, bool rotated = false, int count = 1)
    {
        Data = data;
        GridPosition = gridPos;
        IsRotated = rotated;
        StackCount = Mathf.Clamp(count, 1, data.maxStack);
    }

    /// <summary>
    /// 90도 회전 토글. 1×1 아이템은 회전 무의미.
    /// </summary>
    public void ToggleRotation()
    {
        if (Data.width == Data.height) return; // 정사각형이면 회전 불필요
        IsRotated = !IsRotated;
    }

    /// <summary>
    /// 스택 추가. 성공적으로 추가된 수량을 반환합니다.
    /// 남은(초과) 수량은 호출부에서 별도 처리합니다.
    /// </summary>
    public int AddToStack(int amount)
    {
        int canAdd = Data.maxStack - StackCount;
        int added = Mathf.Min(amount, canAdd);
        StackCount += added;
        return added;
    }

    /// <summary>
    /// 스택에서 수량 제거. 실제로 제거된 수량을 반환합니다.
    /// StackCount가 0이 되면 인벤토리에서 아이템 자체를 제거해야 합니다.
    /// </summary>
    public int RemoveFromStack(int amount)
    {
        int removed = Mathf.Min(amount, StackCount);
        StackCount -= removed;
        return removed;
    }

    /// <summary>
    /// 이 아이템이 차지하는 모든 그리드 좌표를 반환합니다.
    /// </summary>
    public Vector2Int[] GetOccupiedSlots()
    {
        Vector2Int[] slots = new Vector2Int[Width * Height];
        int index = 0;

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                slots[index++] = new Vector2Int(GridPosition.x + x, GridPosition.y + y);
            }
        }

        return slots;
    }
}
