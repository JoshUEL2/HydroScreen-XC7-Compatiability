export function formatUnit(value: number, type: string): string {
    if (value === undefined || value === null) return '--';

    switch (type) {
        case 'Temperature': return `${Math.round(value)}°C`;
        case 'Load': return `${Math.round(value)}%`;
        case 'Clock': return value >= 1000 ? `${(value / 1000).toFixed(1)}GHz` : `${Math.round(value)}MHz`;
        case 'Power': return `${Math.round(value)}W`;
        case 'Voltage': return `${value.toFixed(2)}V`;
        case 'Data':
            if (value >= 1024) return `${(value / 1024).toFixed(1)}TB`;
            if (value >= 1) return `${value.toFixed(1)}GB`;
            return `${Math.round(value * 1024)}MB`;
        case 'SmallData': return `${value.toFixed(1)}GB`;
        case 'Level': return `${Math.round(value)}%`;
        case 'Fan': return `${Math.round(value)} RPM`;
        case 'Flow': return `${Math.round(value)} L/h`;
        case 'Factor': return `${value.toFixed(1)}x`;
        case 'Throughput':
            if (value >= 1024 * 1024) return `${(value / (1024 * 1024)).toFixed(1)}GB/s`;
            if (value >= 1024) return `${(value / 1024).toFixed(1)}MB/s`;
            return `${Math.round(value)}KB/s`;
        case 'Frequency': return value >= 1000 ? `${(value / 1000).toFixed(1)}GHz` : `${Math.round(value)}MHz`;
        case 'TimeSpan': return `${Math.round(value)}s`;
        case 'Energy': return `${value.toFixed(1)}Wh`;
        case 'Noise': return `${value.toFixed(1)}dBA`;
        case 'Current': return `${value.toFixed(2)}A`;
        case 'Conductivity': return `${value.toFixed(1)}µS/cm`;
        case 'Humidity': return `${Math.round(value)}%`;
        default: return `${Math.round(value)}`;
    }
}

export function fitText(
    ctx: CanvasRenderingContext2D,
    text: string,
    maxWidth: number,
    initialFontSize: number,
    fontFamily: string,
    minFontSize: number = 12
): number {
    const savedFont = ctx.font;
    let size = initialFontSize;
    ctx.font = `bold ${size}px "${fontFamily}"`;

    // Quick check — already fits
    if (ctx.measureText(text).width <= maxWidth) return size;

    // Binary search for optimal size
    let lo = minFontSize;
    let hi = initialFontSize;
    while (hi - lo > 1) {
        const mid = Math.floor((lo + hi) / 2);
        ctx.font = `bold ${mid}px "${fontFamily}"`;
        if (ctx.measureText(text).width <= maxWidth) {
            lo = mid;
        } else {
            hi = mid;
        }
    }

    size = lo;
    ctx.font = `bold ${size}px "${fontFamily}"`;
    return size;
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