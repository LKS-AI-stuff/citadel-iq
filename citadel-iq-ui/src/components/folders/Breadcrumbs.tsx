import { Breadcrumbs as MuiBreadcrumbs, Link, Typography } from '@mui/material';
import type { FolderPathSegmentDto } from '../../types/folder';

interface BreadcrumbsProps {
  items: FolderPathSegmentDto[];
  onNavigate: (folderId: string) => void;
}

export function Breadcrumbs({ items, onNavigate }: BreadcrumbsProps) {
  return (
    <MuiBreadcrumbs aria-label="folder breadcrumb">
      {items.map((item, index) => {
        const isLast = index === items.length - 1;

        return isLast ? (
          <Typography key={item.id} color="text.primary" sx={{ fontWeight: 600 }}>
            {item.name}
          </Typography>
        ) : (
          <Link
            key={item.id}
            component="button"
            underline="hover"
            color="inherit"
            onClick={() => onNavigate(item.id)}
          >
            {item.name}
          </Link>
        );
      })}
    </MuiBreadcrumbs>
  );
}
