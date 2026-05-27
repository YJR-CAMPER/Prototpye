using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 인벤토리 매니저.
/// InventoryGrid(로직)와 Unity(게임 오브젝트, 입력)를 연결합니다.
/// 
/// 기획서: "타르코프, 마비노기식 부피형 인벤토리 시스템"
/// 
/// [사용법]
/// 1. 플레이어 오브젝트에 부착합니다.
/// 2. Inspector에서 그리드 크기를 설정합니다.
/// 3. UI 시스템에서 이 매니저를 참조하여 그리드 상태를 렌더링합니다.
/// </summary>
public class InventoryManager : MonoBehaviour
{
    [Header("그리드 설정 (PlayerInv)")]
    [SerializeField] private int gridWidth = 8;
    [SerializeField] private int gridHeight = 6;

    [Header("이벤트")]
    public UnityEvent OnInventoryChanged;   // 아이템 추가/제거/이동 시
    public UnityEvent OnInventoryFull;      // 공간 부족 시

    public InventoryGrid Grid { get; private set; }

    /// <summary> 현재 빈 칸 수 </summary>
    public int FreeSlots => Grid.GetFreeSlotCount();

    /// <summary> 전체 칸 수 </summary>
    public int TotalSlots => gridWidth * gridHeight;

    private void Awake()
    {
        Grid = new InventoryGrid(gridWidth, gridHeight);
    }

    // ──────────────────────────────────
    // 아이템 추가 (주울 때, 구매 시 등)
    // ──────────────────────────────────

    /// <summary>
    /// 아이템을 자동으로 빈 공간에 배치합니다.
    /// 스택 가능하면 기존 스택에 합칩니다.
    /// </summary>
    /// <returns>배치된 InventoryItem, 실패 시 null</returns>
    public InventoryItem AddItem(ItemData data, int count = 1)
    {
        InventoryItem item = Grid.TryAutoPlace(data, count);

        if (item == null)
        {
            OnInventoryFull?.Invoke();
            Debug.Log($"인벤토리 공간 부족: {data.itemName}");
            return null;
        }

        OnInventoryChanged?.Invoke();
        return item;
    }

    /// <summary>
    /// 특정 위치에 아이템을 수동 배치합니다 (드래그 앤 드롭 용).
    /// </summary>
    public bool PlaceItemAt(InventoryItem item, Vector2Int position)
    {
        bool success = Grid.PlaceItem(item, position);

        if (success)
            OnInventoryChanged?.Invoke();

        return success;
    }

    // ──────────────────────────────────
    // 아이템 제거
    // ──────────────────────────────────

    /// <summary>
    /// 아이템을 인벤토리에서 제거합니다 (사용, 버리기, 판매 등).
    /// </summary>
    public bool RemoveItem(InventoryItem item)
    {
        bool success = Grid.RemoveItem(item);

        if (success)
            OnInventoryChanged?.Invoke();

        return success;
    }

    /// <summary>
    /// 스택에서 일정 수량만 제거합니다. 수량이 0이 되면 아이템 자체 제거.
    /// </summary>
    public bool RemoveItemCount(InventoryItem item, int count)
    {
        int removed = item.RemoveFromStack(count);

        if (item.StackCount <= 0)
        {
            Grid.RemoveItem(item);
        }

        if (removed > 0)
        {
            OnInventoryChanged?.Invoke();
            return true;
        }

        return false;
    }

    // ──────────────────────────────────
    // 아이템 이동 / 회전 (UI 드래그 앤 드롭)
    // ──────────────────────────────────

    /// <summary>
    /// 아이템을 새 위치로 이동합니다.
    /// </summary>
    public bool MoveItem(InventoryItem item, Vector2Int newPosition)
    {
        bool success = Grid.MoveItem(item, newPosition);

        if (success)
            OnInventoryChanged?.Invoke();

        return success;
    }

    /// <summary>
    /// 아이템을 제자리에서 회전합니다.
    /// </summary>
    public bool RotateItem(InventoryItem item)
    {
        bool success = Grid.RotateItem(item);

        if (success)
            OnInventoryChanged?.Invoke();

        return success;
    }

    // ──────────────────────────────────
    // 아이템 사용 (소비 아이템)
    // ──────────────────────────────────

    /// <summary>
    /// 소비 아이템을 사용합니다 (기획서: ItemEffect).
    /// </summary>
    public bool UseItem(InventoryItem item, PlayerController player)
    {
        if (item.Data.category != ItemCategory.Consumable)
            return false;

        // 체력 회복 (DamageableBase.Heal)
        if (item.Data.healAmount > 0f)
        {
            player.Heal(item.Data.healAmount);
        }

        // 스태미나 회복 (음수 소모 = 회복)
        if (item.Data.staminaRestore > 0f)
        {
            player.ConsumeStamina(-item.Data.staminaRestore);
        }

        // TODO: 버프 효과 (effectDuration > 0일 때 코루틴 등으로 처리)

        // 수량 감소
        RemoveItemCount(item, 1);

        return true;
    }

    // ──────────────────────────────────
    // 조회
    // ──────────────────────────────────

    /// <summary>
    /// 특정 아이템 데이터의 총 보유 수량을 반환합니다.
    /// </summary>
    public int GetItemCount(ItemData data)
    {
        int total = 0;
        foreach (InventoryItem item in Grid.Items)
        {
            if (item.Data == data)
                total += item.StackCount;
        }
        return total;
    }

    /// <summary>
    /// 특정 좌표에 있는 아이템을 반환합니다.
    /// </summary>
    public InventoryItem GetItemAt(Vector2Int position)
    {
        return Grid.GetItemAt(position);
    }

    /// <summary>
    /// 특정 아이템을 배치할 공간이 있는지 미리 확인합니다.
    /// </summary>
    public bool HasSpaceFor(ItemData data)
    {
        // 스택 가능한 기존 아이템이 있는지 먼저 체크
        if (data.maxStack > 1)
        {
            foreach (InventoryItem existing in Grid.Items)
            {
                if (existing.Data == data && existing.StackCount < data.maxStack)
                    return true;
            }
        }

        // 빈 공간 체크 (일반 + 회전)
        if (Grid.FindFreePosition(data.width, data.height) != null)
            return true;

        if (data.width != data.height &&
            Grid.FindFreePosition(data.height, data.width) != null)
            return true;

        return false;
    }

    // ──────────────────────────────────
    // 그리드 크기 변경 (업그레이드 등)
    // ──────────────────────────────────

    /// <summary>
    /// 그리드 크기를 확장합니다. 기존 아이템은 유지됩니다.
    /// 기획서: PlayerInv(인벤토리 칸 수)를 변경할 때 사용.
    /// </summary>
    public void ExpandGrid(int newWidth, int newHeight)
    {
        if (newWidth < gridWidth || newHeight < gridHeight)
        {
            Debug.LogWarning("축소는 지원하지 않습니다.");
            return;
        }

        InventoryGrid newGrid = new InventoryGrid(newWidth, newHeight);

        // 기존 아이템 재배치
        foreach (InventoryItem item in Grid.Items)
        {
            newGrid.PlaceItem(item, item.GridPosition);
        }

        gridWidth = newWidth;
        gridHeight = newHeight;
        Grid = newGrid;

        OnInventoryChanged?.Invoke();
    }

    // ──────────────────────────────────
    // 디버그
    // ──────────────────────────────────

    /// <summary>
    /// 콘솔에 그리드 상태를 텍스트로 출력합니다.
    /// </summary>
    [ContextMenu("Print Grid")]
    public void DebugPrintGrid()
    {
        string output = $"=== Inventory ({gridWidth}×{gridHeight}) ===\n";

        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                InventoryItem item = Grid.GetItemAt(new Vector2Int(x, y));
                output += item != null ? "[■]" : "[　]";
            }
            output += "\n";
        }

        output += $"Items: {Grid.Items.Count} | Free: {FreeSlots}/{TotalSlots}";
        Debug.Log(output);
    }
}
