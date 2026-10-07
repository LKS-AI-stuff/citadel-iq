import { Typography } from '@mui/material';
import { useSession } from '../../session/useSession';
import type { UploaderDto } from '../../types/folder';

/** "Uploaded by Name" (or "Name (former member)") — organizations only; in a personal workspace it's always you. */
export function UploaderCaption({ uploadedBy }: { uploadedBy: UploaderDto | null }) {
  const { session } = useSession();
  if (!uploadedBy || session?.workspace?.kind !== 'Organization') {
    return null;
  }

  return (
    <Typography variant="caption" color="text.secondary" noWrap sx={{ display: 'block' }}>
      Uploaded by {uploadedBy.displayName}
      {uploadedBy.isFormerMember ? ' (former member)' : ''}
    </Typography>
  );
}
