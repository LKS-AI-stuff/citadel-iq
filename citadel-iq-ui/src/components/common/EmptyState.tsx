import { Box, Typography } from '@mui/material';
import FolderOpenOutlinedIcon from '@mui/icons-material/FolderOpenOutlined';

interface EmptyStateProps {
  title: string;
  description?: string;
}

export function EmptyState({ title, description }: EmptyStateProps) {
  return (
    <Box
      sx={{
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        textAlign: 'center',
        py: 10,
        color: 'text.secondary',
      }}
    >
      <FolderOpenOutlinedIcon sx={{ fontSize: 56, mb: 2, opacity: 0.5 }} />
      <Typography variant="h6" color="text.primary" gutterBottom>
        {title}
      </Typography>
      {description ? <Typography variant="body2">{description}</Typography> : null}
    </Box>
  );
}
