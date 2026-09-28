import { IconButton, InputAdornment, TextField, alpha } from '@mui/material';
import SearchIcon from '@mui/icons-material/Search';
import ClearIcon from '@mui/icons-material/Clear';

interface SearchInputProps {
  value: string;
  onChange: (value: string) => void;
  onSearch: () => void;
  onClear: () => void;
  isLoading: boolean;
}

export function SearchInput({ value, onChange, onSearch, onClear, isLoading }: SearchInputProps) {
  return (
    <TextField
      fullWidth
      autoFocus
      placeholder="How many vacation days are employees allowed?"
      value={value}
      onChange={(e) => onChange(e.target.value)}
      onKeyDown={(e) => {
        if (e.key === 'Enter') {
          onSearch();
        }
      }}
      sx={{
        '& .MuiOutlinedInput-root': {
          borderRadius: '12px',
          fontSize: 15,
          backgroundColor: (t) => alpha(t.palette.text.primary, t.palette.mode === 'light' ? 0.04 : 0.08),
          backdropFilter: 'blur(8px)',
          '& fieldset': {
            borderColor: (t) => alpha(t.palette.text.primary, 0.14),
          },
          '&:hover fieldset': {
            borderColor: (t) => alpha(t.palette.primary.main, 0.5),
          },
        },
      }}
      slotProps={{
        input: {
          startAdornment: (
            <InputAdornment position="start">
              <SearchIcon color="action" />
            </InputAdornment>
          ),
          endAdornment: value ? (
            <InputAdornment position="end">
              <IconButton size="small" onClick={onClear} aria-label="Clear search">
                <ClearIcon fontSize="small" />
              </IconButton>
            </InputAdornment>
          ) : undefined,
        },
      }}
      disabled={isLoading}
    />
  );
}
