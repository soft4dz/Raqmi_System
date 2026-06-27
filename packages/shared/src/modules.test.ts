import { describe, expect, it } from 'vitest';
import { RAQMI_MODULES, isKnownRaqmiModule } from './modules';

describe('RAQMI_MODULES', () => {
  it('définit 23 modules', () => {
    expect(RAQMI_MODULES).toHaveLength(23);
  });

  it('inclut les modules core non commerciaux', () => {
    const core = RAQMI_MODULES.filter((m) => m.family === 'core' && !m.commercial);
    expect(core.map((m) => m.code)).toEqual(['administration', 'settings']);
  });

  it('reconnaît un code module valide', () => {
    expect(isKnownRaqmiModule('billing')).toBe(true);
    expect(isKnownRaqmiModule('inconnu')).toBe(false);
  });
});
