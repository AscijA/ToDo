export function attachSortable(container, dotNetRef, draggable, handle) {
    const { Sortable } = window.Draggable;

    const options = {
        draggable: draggable,
        mirror: { constrainDimensions: true },
    };

    if (handle) {
        options.handle = handle;
        options.delay = { mouse: 0, drag: 0, touch: 100 };
    } else {
        options.distance = 8;
        options.delay = { mouse: 150, drag: 0, touch: 200 };
    }

    const sortable = new Sortable(container, options);

    sortable.on("sortable:stop", (evt) => {
        const oldIndex = evt.data.oldIndex;
        const newIndex = evt.data.newIndex;
        if (oldIndex !== newIndex) {
            dotNetRef.invokeMethodAsync("OnReordered", oldIndex, newIndex);
        }
    });

    return {
        dispose() {
            sortable.destroy();
        }
    };
}
