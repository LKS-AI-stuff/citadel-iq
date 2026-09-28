import { useRef, useState } from 'react';
import { Box, Button, Typography, alpha } from '@mui/material';
import CloudUploadOutlinedIcon from '@mui/icons-material/CloudUploadOutlined';

interface UploadDropzoneProps {
  onFilesSelected: (files: FileList) => void;
}

export function UploadDropzone({ onFilesSelected }: UploadDropzoneProps) {
  const [isDragActive, setIsDragActive] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);

  return (
    <Box
      onDragOver={(e) => {
        e.preventDefault();
        setIsDragActive(true);
      }}
      onDragLeave={() => setIsDragActive(false)}
      onDrop={(e) => {
        e.preventDefault();
        setIsDragActive(false);
        if (e.dataTransfer.files.length > 0) {
          onFilesSelected(e.dataTransfer.files);
        }
      }}
      sx={{
        border: '2px dashed',
        borderColor: isDragActive ? 'primary.main' : 'divider',
        borderRadius: '16px',
        bgcolor: (t) => (isDragActive ? alpha(t.palette.primary.main, 0.06) : 'transparent'),
        p: 5,
        textAlign: 'center',
        transition: 'all 0.2s cubic-bezier(0.16, 1, 0.3, 1)',
        transform: isDragActive ? 'scale(1.01)' : 'scale(1)',
      }}
    >
      <Box
        sx={{
          width: 56,
          height: 56,
          mx: 'auto',
          mb: 1.5,
          borderRadius: '50%',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          bgcolor: (t) => alpha(t.palette.primary.main, t.palette.mode === 'light' ? 0.08 : 0.16),
          transition: 'transform 0.25s cubic-bezier(0.34, 1.56, 0.64, 1)',
          transform: isDragActive ? 'scale(1.12) translateY(-4px)' : 'scale(1)',
        }}
      >
        <CloudUploadOutlinedIcon sx={{ fontSize: 28, color: 'primary.main' }} />
      </Box>
      <Typography variant="body1" gutterBottom sx={{ fontWeight: 600 }}>
        Drag and drop files here
      </Typography>
      <Typography variant="body2" color="text.secondary" gutterBottom>
        PDF, DOCX, TXT, CSV, or XLSX
      </Typography>
      <Button variant="outlined" size="small" sx={{ mt: 1 }} onClick={() => inputRef.current?.click()}>
        Browse files
      </Button>
      <input
        ref={inputRef}
        type="file"
        multiple
        hidden
        accept=".pdf,.docx,.txt,.csv,.xlsx"
        onChange={(e) => {
          if (e.target.files && e.target.files.length > 0) {
            onFilesSelected(e.target.files);
            e.target.value = '';
          }
        }}
      />
    </Box>
  );
}
