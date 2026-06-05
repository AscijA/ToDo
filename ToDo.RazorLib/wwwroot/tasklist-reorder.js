export function attachTaskListReorder(container, dotNetRef) {
    let draggedRow = null;
    let draggedId = null;
    let targetRow = null;
    let placeholder = null;
    let placement = "before";

    const clearTarget = () => {
        if (targetRow) {
            targetRow.classList.remove("tasklist-drop-target");
        }
        targetRow = null;
    };

    const removePlaceholder = () => {
        placeholder?.remove();
        placeholder = null;
    };

    const cleanup = () => {
        clearTarget();
        removePlaceholder();
        if (draggedRow) {
            draggedRow.classList.remove("tasklist-item-dragging");
        }
        draggedRow = null;
        draggedId = null;
        placement = "before";
        dotNetRef.invokeMethodAsync("ClearDraggingItem");
    };

    const findRow = element => element?.closest?.("[data-task-list-item-id]");

    const ensurePlaceholder = () => {
        if (placeholder) {
            return placeholder;
        }

        placeholder = document.createElement("div");
        placeholder.className = "tasklist-drop-placeholder";
        placeholder.style.minHeight = "40px";
        placeholder.style.borderWidth = "2px";
        placeholder.style.borderStyle = "dashed";
        placeholder.style.borderColor = "var(--mud-palette-primary)";
        placeholder.style.borderRadius = "4px";
        placeholder.style.backgroundColor = "rgba(89, 74, 226, .12)";
        placeholder.style.boxSizing = "border-box";
        placeholder.style.pointerEvents = "none";
        placeholder.style.width = "100%";
        if (draggedRow) {
            placeholder.style.height = `${draggedRow.getBoundingClientRect().height}px`;
        }

        return placeholder;
    };

    const movePlaceholder = row => {
        const marker = ensurePlaceholder();
        if (placement === "after") {
            row.after(marker);
            return;
        }

        row.before(marker);
    };

    const onPointerDown = event => {
        const handle = event.target.closest(".tasklist-drag-handle");
        if (!handle || event.button !== 0) {
            return;
        }

        const row = findRow(handle);
        if (!row) {
            return;
        }

        event.preventDefault();
        handle.setPointerCapture?.(event.pointerId);
        draggedRow = row;
        draggedId = row.dataset.taskListItemId;
        row.classList.add("tasklist-item-dragging");
        dotNetRef.invokeMethodAsync("SetDraggingItem", draggedId);
    };

    const onPointerMove = event => {
        if (!draggedRow) {
            return;
        }

        const hoveredElement = document.elementFromPoint(event.clientX, event.clientY);
        if (hoveredElement === placeholder) {
            return;
        }

        const row = findRow(hoveredElement);
        if (!row || row === draggedRow) {
            clearTarget();
            return;
        }

        if (targetRow !== row) {
            clearTarget();
            targetRow = row;
            targetRow.classList.add("tasklist-drop-target");
        }

        const rect = row.getBoundingClientRect();
        placement = event.clientY > rect.top + rect.height / 2 ? "after" : "before";
        movePlaceholder(row);
    };

    const onPointerUp = event => {
        if (!draggedRow) {
            return;
        }

        event.preventDefault();
        const sourceId = draggedId;
        const targetId = targetRow?.dataset.taskListItemId;
        if (sourceId && targetId && sourceId !== targetId) {
            dotNetRef.invokeMethodAsync("MoveItemById", sourceId, targetId, placement);
        }

        cleanup();
    };

    const onPointerCancel = () => {
        if (draggedRow) {
            cleanup();
        }
    };

    container.addEventListener("pointerdown", onPointerDown);
    window.addEventListener("pointermove", onPointerMove);
    window.addEventListener("pointerup", onPointerUp);
    window.addEventListener("pointercancel", onPointerCancel);

    return {
        dispose() {
            container.removeEventListener("pointerdown", onPointerDown);
            window.removeEventListener("pointermove", onPointerMove);
            window.removeEventListener("pointerup", onPointerUp);
            window.removeEventListener("pointercancel", onPointerCancel);
        }
    };
}
