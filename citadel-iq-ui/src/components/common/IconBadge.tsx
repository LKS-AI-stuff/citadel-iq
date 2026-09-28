import { Box, alpha } from '@mui/material';
import type { ReactNode } from 'react';

interface IconBadgeProps {
  color: string;
  children: ReactNode;
  size?: number;
  className?: string;
}

export function IconBadge({ color, children, size = 44, className }: IconBadgeProps) {
  return (
    <Box
      className={className}
      sx={{
        width: size,
        height: size,
        flexShrink: 0,
        borderRadius: '12px',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        bgcolor: alpha(color, 0.12),
        transition: 'transform 0.2s cubic-bezier(0.34, 1.56, 0.64, 1)',
      }}
    >
      {children}
    </Box>
  );
}
