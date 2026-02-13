export function formatUnit(value: number, type: string): string {
    if (value === undefined || value === null) return '--';
    const v = Math.round(value);

    switch (type) {
        case 'Temperature': return `${v}°C`;
        case 'Load': return `${v}%`;
        case 'Clock': return value > 1000 ? `${(value / 1000).toFixed(1)}GHz` : `${v}MHz`;
        case 'Power': return `${v}W`;
        case 'Voltage': return `${value.toFixed(2)}V`;
        case 'Data': return `${v}GB`; // Simplified
        case 'Level': return `${v}%`;
        case 'Fan': return `${v} RPM`;
        case 'Flow': return `${v} L/h`;
        default: return `${v}`;
    }
}

export function fitText(
    ctx: CanvasRenderingContext2D,
    text: string,
    maxWidth: number,
    initialFontSize: number,
    fontFamily: string,
    minFontSize: number = 12
): void {
    let size = initialFontSize;
    ctx.font = `bold ${size}px "${fontFamily}"`;

    // Quick check
    if (ctx.measureText(text).width <= maxWidth) return;

    // Binaryish search or iterative reduction
    while (size > minFontSize) {
        size -= 2;
        ctx.font = `bold ${size}px "${fontFamily}"`;
        if (ctx.measureText(text).width <= maxWidth) break;
    }
}

export function getDefaultRange(type: string): { min: number, max: number } {
    let max = 100;
    switch (type) {
        case "Fan":
            max = 2500;
            break;
        case "Flow":
            max = 200;
            break;
        case "Clock":
            max = 6000;
            break;
        default:
            max = 100;
            break;
    }
    return { min: 0, max };
}