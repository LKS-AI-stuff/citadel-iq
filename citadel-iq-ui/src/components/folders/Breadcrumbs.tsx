import { Breadcrumbs as MuiBreadcrumbs, Link, Stack, Typography } from '@mui/material';
import HomeRoundedIcon from '@mui/icons-material/HomeRounded';
import NavigateNextRoundedIcon from '@mui/icons-material/NavigateNextRounded';
import type { FolderPathSegmentDto } from '../../types/folder';

interface BreadcrumbsProps {
  items: FolderPathSegmentDto[];
  onNavigate: (folderId: string) => void;
}

export function Breadcrumbs({ items, onNavigate }: BreadcrumbsProps) {
  return (
    <MuiBreadcrumbs
      aria-label="folder breadcrumb"
      separator={<NavigateNextRoundedIcon sx={{ color: 'text.disabled', fontSize: 20 }} />}
    >
      {items.map((item, index) => {
        const isLast = index === items.length - 1;
        const isRoot = index === 0;

        const label = (
          <Stack direction="row" sx={{ alignItems: 'center', gap: 0.5 }}>
            {isRoot && <HomeRoundedIcon sx={{ fontSize: 20 }} />}
            <span>{item.name}</span>
          </Stack>
        );

        return isLast ? (
          <Typography key={item.id} component="span" color="text.primary" variant="body1" sx={{ fontWeight: 700 }}>
            {label}
          </Typography>
        ) : (
          <Link
            key={item.id}
            component="button"
            underline="none"
            variant="body1"
            color="text.secondary"
            onClick={() => onNavigate(item.id)}
            sx={{
              fontWeight: 500,
              transition: 'color 0.15s ease',
              '&:hover': { color: 'primary.main' },
            }}
          >
            {label}
          </Link>
        );
      })}
    </MuiBreadcrumbs>
  );
}
