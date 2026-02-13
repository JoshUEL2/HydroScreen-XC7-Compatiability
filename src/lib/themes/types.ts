import type { SensorType } from '../types';

export interface ThemeSlot {
    id: string;
    label: string;
    type: 'number' | 'text'; // Future proofing
    allowedTypes?: SensorType[];
}

export interface ThemeOption {
    id: string;
    label: string;
    type: 'color' | 'boolean' | 'range' | 'file' | 'text' | 'font';
    default: any;
    min?: number;
    max?: number;
}

/**
 * The Render Function Signature
 * @param ctx The Canvas Context
 * @param w Width (480)
 * @param h Height (480)
 * @param values The sensor data mapped by the user
 * @param formatted Formatted string values
 * @param config Theme configuration values
 * @param tick A continuous counter (0, 1, 2...) for calculating animations
 * @param assets Loaded assets (images, etc.)
 */
export type RenderFunction = (
    ctx: CanvasRenderingContext2D,
    w: number,
    h: number,
    values: Record<string, number>,
    formatted: Record<string, string>,
    config: Record<string, any>,
    tick: number,
    assets: Record<string, any>
) => void;

export interface ThemeDefinition {
    id: string;
    name: string;
    author: string;      // Credit the creator
    description: string;
    slots: ThemeSlot[];
    options: ThemeOption[];
    renderFn: RenderFunction;
}