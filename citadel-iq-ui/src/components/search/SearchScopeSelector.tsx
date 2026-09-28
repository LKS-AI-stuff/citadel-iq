import { ToggleButton, ToggleButtonGroup, Typography, alpha } from '@mui/material';
import ApartmentRoundedIcon from '@mui/icons-material/ApartmentRounded';
import FolderRoundedIcon from '@mui/icons-material/FolderRounded';
import AccountTreeRoundedIcon from '@mui/icons-material/AccountTreeRounded';
import type { ReactNode } from 'react';
import type { SearchScope } from '../../types/search';

interface SearchScopeSelectorProps {
  value: SearchScope;
  onChange: (scope: SearchScope) => void;
}

const OPTIONS: { value: SearchScope; label: string; icon: ReactNode }[] = [
  { value: 'CurrentFolder', label: 'This folder', icon: <FolderRoundedIcon fontSize="small" /> },
  { value: 'CurrentFolderAndSubfolders', label: '+ Subfolders', icon: <AccountTreeRoundedIcon fontSize="small" /> },
  { value: 'EntirePortal', label: 'Entire portal', icon: <ApartmentRoundedIcon fontSize="small" /> },
];

export function SearchScopeSelector({ value, onChange }: SearchScopeSelectorProps) {
  return (
    <div>
      <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 600, letterSpacing: '0.04em', mb: 0.75, display: 'block' }}>
        SEARCH SCOPE
      </Typography>
      <ToggleButtonGroup
        value={value}
        exclusive
        onChange={(_, next) => {
          if (next) onChange(next as SearchScope);
        }}
        aria-label="Search scope"
        fullWidth
        size="small"
        sx={{
          '& .MuiToggleButton-root': {
            textTransform: 'none',
            fontWeight: 600,
            fontSize: 12.5,
            gap: 0.6,
            py: 0.9,
            borderColor: (t) => alpha(t.palette.text.primary, 0.14),
            backgroundColor: (t) => alpha(t.palette.text.primary, t.palette.mode === 'light' ? 0.03 : 0.06),
            backdropFilter: 'blur(8px)',
            '&.Mui-selected': {
              backgroundColor: (t) => alpha(t.palette.primary.main, t.palette.mode === 'light' ? 0.14 : 0.24),
              '&:hover': {
                backgroundColor: (t) => alpha(t.palette.primary.main, t.palette.mode === 'light' ? 0.18 : 0.28),
              },
            },
          },
        }}
      >
        {OPTIONS.map((option) => (
          <ToggleButton key={option.value} value={option.value} aria-label={option.label}>
            {option.icon}
            {option.label}
          </ToggleButton>
        ))}
      </ToggleButtonGroup>
    </div>
  );
}
