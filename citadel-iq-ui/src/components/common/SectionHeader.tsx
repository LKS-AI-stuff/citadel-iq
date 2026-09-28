import { Chip, Stack, Typography } from '@mui/material';
import type { ReactNode } from 'react';
import { IconBadge } from './IconBadge';

interface SectionHeaderProps {
  icon: ReactNode;
  color: string;
  title: string;
  count: number;
  action?: ReactNode;
}

export function SectionHeader({ icon, color, title, count, action }: SectionHeaderProps) {
  return (
    <Stack direction="row" sx={{ alignItems: 'center', gap: 1.5, mb: 2.5, flexWrap: 'wrap' }}>
      <IconBadge color={color} size={36}>
        {icon}
      </IconBadge>
      <Typography variant="h5" sx={{ flexShrink: 0 }}>
        {title}
      </Typography>
      <Chip size="small" label={count} sx={{ fontWeight: 700 }} />
      <Stack sx={{ flex: 1 }} />
      {action}
    </Stack>
  );
}
