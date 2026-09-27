import { Box, Skeleton, Stack } from '@mui/material';

export function LoadingState() {
  return (
    <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(220px, 1fr))', gap: 2, py: 2 }}>
      {Array.from({ length: 6 }).map((_, index) => (
        <Stack key={index} spacing={1}>
          <Skeleton variant="rounded" height={90} />
          <Skeleton variant="text" width="60%" />
        </Stack>
      ))}
    </Box>
  );
}
