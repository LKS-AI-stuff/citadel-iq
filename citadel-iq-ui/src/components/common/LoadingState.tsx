import { Box, Paper, Skeleton, Stack } from '@mui/material';

export function LoadingState() {
  return (
    <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(240px, 1fr))', gap: 2, py: 0.5 }}>
      {Array.from({ length: 8 }).map((_, index) => (
        <Paper
          key={index}
          variant="outlined"
          className="animate-fade-in"
          sx={{ p: 2, display: 'flex', alignItems: 'center', gap: 1.75, animationDelay: `${index * 30}ms` }}
        >
          <Skeleton variant="rounded" width={44} height={44} sx={{ borderRadius: '12px', flexShrink: 0 }} />
          <Stack spacing={0.75} sx={{ flex: 1 }}>
            <Skeleton variant="text" width="70%" height={20} />
            <Skeleton variant="text" width="40%" height={14} />
          </Stack>
        </Paper>
      ))}
    </Box>
  );
}
