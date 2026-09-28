import { Box, Typography, alpha } from '@mui/material';
import FolderOpenOutlinedIcon from '@mui/icons-material/FolderOpenOutlined';
import type { ReactNode } from 'react';

interface EmptyStateProps {
  title: string;
  description?: string;
  icon?: ReactNode;
  dense?: boolean;
}

export function EmptyState({ title, description, icon, dense = false }: EmptyStateProps) {
  return (
    <Box
      className="animate-fade-in"
      sx={{
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        textAlign: 'center',
        py: dense ? 4 : 10,
        px: 3,
        color: 'text.secondary',
      }}
    >
      <Box
        sx={{
          width: dense ? 48 : 72,
          height: dense ? 48 : 72,
          borderRadius: '50%',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          mb: dense ? 1.5 : 2.5,
          bgcolor: (t) => alpha(t.palette.primary.main, t.palette.mode === 'light' ? 0.08 : 0.14),
        }}
      >
        {icon ?? <FolderOpenOutlinedIcon sx={{ fontSize: dense ? 22 : 32, color: 'primary.main' }} />}
      </Box>
      <Typography variant={dense ? 'body2' : 'h6'} color={dense ? 'text.secondary' : 'text.primary'} gutterBottom sx={{ fontWeight: dense ? 600 : 700 }}>
        {title}
      </Typography>
      {description ? <Typography variant="body2" sx={{ maxWidth: 360 }}>{description}</Typography> : null}
    </Box>
  );
}
