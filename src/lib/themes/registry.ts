import type { ThemeDefinition } from '../types';

import { theme as CustomMedia } from './library/custom_media';
import { theme as HydroGauge } from './library/hydro_gauge';
import { theme as TerminalZero } from './library/terminal_zero';
import { theme as Quantum } from './library/quantum';
import { theme as LiquidFlow } from './library/liquid_flow';

export const themes: ThemeDefinition[] = [
    CustomMedia,
    HydroGauge,
    LiquidFlow,
    Quantum,
    TerminalZero
];

/**
 * == THEME REGISTRY ==
 * 
 * Contributor Guide:
 * 1. Create a definition file in `./library/<theme_name>.ts`
 * 2. Implement the `ThemeDefinition` interface.
 * 3. Import the theme module below.
 * 4. Add it to the `themes` array constant.
 */