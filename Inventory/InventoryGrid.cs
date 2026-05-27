using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 부피형 인벤토리 그리드.
/// 타르코프/마비노기식 2D 격자에서 아이템 배치, 충돌 검사, 자동 배치를 관리합니다.
/// 
/// 기획서: "단순히 갯수의 문제가 아니라, 어떻게 넣느냐에 따라 아이템을 더 넣을 수 있음"
/// </summary>
public class InventoryGrid
{
    /// <summary> 그리드 가로 칸 수 </summary>
    public int GridWidth { get; private set; }

    /// <summary> 그리드 세로 칸 수 </summary>
    public int GridHeight { get; private set; }

    /// <summary> 배치된 아이템 목록 </summary>
    public List<InventoryItem> Items { get; private set; }

    // 내부: 각 칸이 어떤 아이템에 점유되어 있는지 추적 (null = 빈 칸)
    private InventoryItem[,] _slotMap;

    public InventoryGrid(int width, int height)
    {
        GridWidth = width;
        GridHeight = height;
        Items = new List<InventoryItem>();
        _slotMap = new InventoryItem[width, height];
    }

    // ──────────────────────────────────
    // 배치 가능 여부 검사
    // ──────────────────────────────────

    /// <summary>
    /// 지정 위치에 아이템을 놓을 수 있는지 확인합니다.
    /// </summary>
    /// <param name="position">좌상단 그리드 좌표</param>
    /// <param name="itemWidth">아이템 가로 칸 수 (회전 반영)</param>
    /// <param name="itemHeight">아이템 세로 칸 수 (회전 반영)</param>
    /// <param name="ignoreItem">이동 중인 아이템 자기 자신은 무시</param>
    public bool CanPlace(Vector2Int position, int itemWidth, int itemHeight, InventoryItem ignoreItem = null)
    {
        // 범위 초과 체크
        if (position.x < 0 || position.y < 0)
            return false;
        if (position.x + itemWidth > GridWidth || position.y + itemHeight > GridHeight)
            return false;

        // 겹침 체크
        for (int y = 0; y < itemHeight; y++)
        {
            for (int x = 0; x < itemWidth; x++)
            {
                InventoryItem occupant = _slotMap[position.x + x, position.y + y];
                if (occupant != null && occupant != ignoreItem)
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// InventoryItem을 직접 넘겨서 현재 회전 상태 기준으로 배치 가능 여부를 확인합니다.
    /// </summary>
    public bool CanPlace(Vector2Int position, InventoryItem item)
    {
        return CanPlace(position, item.Width, item.Height, item);
    }

    // ──────────────────────────────────
    // 아이템 배치
    // ──────────────────────────────────

    /// <summary>
    /// 지정 위치에 아이템을 배치합니다. 배치 전 CanPlace 검사 필수.
    /// </summary>
    public bool PlaceItem(InventoryItem item, Vector2Int position)
    {
        if (!CanPlace(position, item))
            return false;

        item.GridPosition = position;
        RegisterSlots(item);

        if (!Items.Contains(item))
            Items.Add(item);

        return true;
    }

    /// <summary>
    /// 아이템 데이터로 새 아이템을 생성하여 지정 위치에 배치합니다.
    /// </summary>
    public InventoryItem PlaceNewItem(ItemData data, Vector2Int position, bool rotated = false, int count = 1)
    {
        InventoryItem item = new InventoryItem(data, position, rotated, count);

        if (!PlaceItem(item, position))
            return null;

        return item;
    }

    // ──────────────────────────────────
    // 아이템 제거
    // ──────────────────────────────────

    /// <summary>
    /// 아이템을 그리드에서 제거합니다.
    /// </summary>
    public bool RemoveItem(InventoryItem item)
    {
        if (!Items.Contains(item))
            return false;

        ClearSlots(item);
        Items.Remove(item);
        return true;
    }

    /// <summary>
    /// 특정 좌표에 있는 아이템을 반환합니다.
    /// </summary>
    public InventoryItem GetItemAt(Vector2Int position)
    {
        if (position.x < 0 || position.x >= GridWidth ||
            position.y < 0 || position.y >= GridHeight)
            return null;

        return _slotMap[position.x, position.y];
    }

    // ──────────────────────────────────
    // 아이템 이동 / 회전
    // ──────────────────────────────────

    /// <summary>
    /// 아이템을 새 위치로 이동합니다.
    /// </summary>
    public bool MoveItem(InventoryItem item, Vector2Int newPosition)
    {
        ClearSlots(item);

        if (CanPlace(newPosition, item.Width, item.Height))
        {
            item.GridPosition = newPosition;
            RegisterSlots(item);
            return true;
        }

        // 이동 실패 시 원래 위치로 복구
        RegisterSlots(item);
        return false;
    }

    /// <summary>
    /// 아이템을 제자리에서 회전합니다.
    /// 회전 후 배치 불가능하면 원래 상태로 복구합니다.
    /// </summary>
    public bool RotateItem(InventoryItem item)
    {
        if (item.Data.width == item.Data.height) return false; // 정사각형 회전 무의미

        ClearSlots(item);
        item.ToggleRotation();

        if (CanPlace(item.GridPosition, item.Width, item.Height))
        {
            RegisterSlots(item);
            return true;
        }

        // 실패 시 복구
        item.ToggleRotation();
        RegisterSlots(item);
        return false;
    }

    // ──────────────────────────────────
    // 자동 배치 (주울 때 등)
    // ──────────────────────────────────

    /// <summary>
    /// 같은 데이터의 기존 스택에 먼저 합치고, 남으면 빈 공간을 찾아 자동 배치합니다.
    /// 배치 성공한 아이템을 반환합니다. 공간 부족 시 null.
    /// </summary>
    public InventoryItem TryAutoPlace(ItemData data, int count = 1)
    {
        int remaining = count;

        // 1단계: 기존 스택에 합치기
        if (data.maxStack > 1)
        {
            foreach (InventoryItem existing in Items)
            {
                if (existing.Data == data && existing.StackCount < data.maxStack)
                {
                    int added = existing.AddToStack(remaining);
                    remaining -= added;
                    if (remaining <= 0)
                        return existing;
                }
            }
        }

        // 2단계: 빈 공간 찾기 (회전 없이 먼저, 실패 시 회전)
        Vector2Int? pos = FindFreePosition(data.width, data.height);
        bool rotated = false;

        if (pos == null && data.width != data.height)
        {
            pos = FindFreePosition(data.height, data.width);
            rotated = true;
        }

        if (pos == null)
            return null;

        return PlaceNewItem(data, pos.Value, rotated, remaining);
    }

    /// <summary>
    /// 지정 크기의 아이템이 들어갈 수 있는 첫 번째 빈 좌표를 찾습니다.
    /// 좌상단부터 행 우선 탐색 (왼쪽→오른쪽, 위→아래).
    /// </summary>
    public Vector2Int? FindFreePosition(int itemWidth, int itemHeight)
    {
        for (int y = 0; y <= GridHeight - itemHeight; y++)
        {
            for (int x = 0; x <= GridWidth - itemWidth; x++)
            {
                Vector2Int pos = new Vector2Int(x, y);
                if (CanPlace(pos, itemWidth, itemHeight))
                    return pos;
            }
        }

        return null;
    }

    // ──────────────────────────────────
    // 유틸리티
    // ──────────────────────────────────

    /// <summary>
    /// 그리드에 빈 칸이 총 몇 칸인지 반환합니다.
    /// </summary>
    public int GetFreeSlotCount()
    {
        int count = 0;
        for (int y = 0; y < GridHeight; y++)
            for (int x = 0; x < GridWidth; x++)
                if (_slotMap[x, y] == null)
                    count++;
        return count;
    }

    /// <summary>
    /// 그리드를 완전히 비웁니다.
    /// </summary>
    public void Clear()
    {
        Items.Clear();
        _slotMap = new InventoryItem[GridWidth, GridHeight];
    }

    // ──────────────────────────────────
    // 내부 슬롯맵 관리
    // ──────────────────────────────────

    private void RegisterSlots(InventoryItem item)
    {
        Vector2Int[] slots = item.GetOccupiedSlots();
        foreach (Vector2Int slot in slots)
        {
            _slotMap[slot.x, slot.y] = item;
        }
    }

    private void ClearSlots(InventoryItem item)
    {
        Vector2Int[] slots = item.GetOccupiedSlots();
        foreach (Vector2Int slot in slots)
        {
            if (_slotMap[slot.x, slot.y] == item)
                _slotMap[slot.x, slot.y] = null;
        }
    }
}
