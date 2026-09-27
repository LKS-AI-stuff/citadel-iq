import { FormControl, FormControlLabel, Radio, RadioGroup } from '@mui/material';
import type { SearchScope } from '../../types/search';

interface SearchScopeSelectorProps {
  value: SearchScope;
  onChange: (scope: SearchScope) => void;
}

const OPTIONS: { value: SearchScope; label: string }[] = [
  { value: 'EntirePortal', label: 'Entire portal' },
  { value: 'CurrentFolder', label: 'Current folder' },
  { value: 'CurrentFolderAndSubfolders', label: 'Current folder + subfolders' },
];

export function SearchScopeSelector({ value, onChange }: SearchScopeSelectorProps) {
  return (
    <FormControl>
      <RadioGroup value={value} onChange={(e) => onChange(e.target.value as SearchScope)}>
        {OPTIONS.map((option) => (
          <FormControlLabel key={option.value} value={option.value} control={<Radio size="small" />} label={option.label} />
        ))}
      </RadioGroup>
    </FormControl>
  );
}
