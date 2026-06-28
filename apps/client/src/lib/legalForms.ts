export type LegalFormOption = {
  value: string;
  label: string;
  group: string;
};

/** Formes juridiques courantes en Algérie (personnes physiques et morales). */
export const ALGERIAN_LEGAL_FORMS: LegalFormOption[] = [
  // Personnes physiques
  { group: 'Personne physique', value: 'personne_physique', label: 'Personne physique — activité commerciale' },
  { group: 'Personne physique', value: 'auto_entrepreneur', label: 'Auto-entrepreneur (loi 22-23)' },
  { group: 'Personne physique', value: 'ei', label: 'Entreprise individuelle (EI)' },
  { group: 'Personne physique', value: 'artisan', label: 'Artisan inscrit au registre' },
  { group: 'Personne physique', value: 'profession_liberale', label: 'Profession libérale / activité non commerciale' },

  // Sociétés de personnes
  { group: 'Société de personnes', value: 'snc', label: 'SNC — Société en nom collectif' },
  { group: 'Société de personnes', value: 'scs', label: 'SCS — Société en commandite simple' },

  // Sociétés à responsabilité limitée
  { group: 'Société à responsabilité limitée', value: 'sarl', label: 'SARL — Société à responsabilité limitée' },
  { group: 'Société à responsabilité limitée', value: 'eurl', label: 'EURL — Entreprise unipersonnelle à responsabilité limitée' },

  // Sociétés par actions
  { group: 'Société par actions', value: 'spa', label: 'SPA — Société par actions' },
  { group: 'Société par actions', value: 'sca', label: 'SCA — Société en commandite par actions' },

  // Autres structures
  { group: 'Autres structures', value: 'gie', label: 'GIE — Groupement d\'intérêt économique' },
  { group: 'Autres structures', value: 'cooperative', label: 'Coopérative' },
  { group: 'Autres structures', value: 'association', label: 'Association (à but non lucratif)' },
  { group: 'Autres structures', value: 'epic', label: 'EPIC — Établissement public industriel et commercial' },
  { group: 'Autres structures', value: 'epa', label: 'EPA — Établissement public administratif' },
  { group: 'Autres structures', value: 'epst', label: 'EPST — Établissement public scientifique et technologique' },
  { group: 'Autres structures', value: 'autre', label: 'Autre (préciser en commentaire interne)' },
];

export const LEGAL_FORM_GROUPS = [...new Set(ALGERIAN_LEGAL_FORMS.map((f) => f.group))];

export function legalFormLabel(value?: string | null): string {
  if (!value) return '—';
  const found = ALGERIAN_LEGAL_FORMS.find((f) => f.value === value);
  if (found) return found.label;
  // Rétrocompatibilité anciennes valeurs en majuscules
  const legacy = ALGERIAN_LEGAL_FORMS.find((f) => f.value === value.toLowerCase());
  if (legacy) return legacy.label;
  return value;
}

export function normalizeLegalFormValue(value?: string | null): string {
  if (!value) return 'sarl';
  const lower = value.toLowerCase();
  if (ALGERIAN_LEGAL_FORMS.some((f) => f.value === lower)) return lower;
  const legacyMap: Record<string, string> = {
    sarl: 'sarl',
    eurl: 'eurl',
    spa: 'spa',
    snc: 'snc',
    'auto-entrepreneur': 'auto_entrepreneur',
  };
  return legacyMap[lower] ?? value;
}
