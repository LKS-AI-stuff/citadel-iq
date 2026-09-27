import { useRef, useState } from 'react';
import { Box, Button, Typography } from '@mui/material';
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
        borderRadius: 2,
        bgcolor: isDragActive ? 'action.hover' : 'transparent',
        p: 4,
        textAlign: 'center',
        transition: 'all 0.15s ease',
      }}
    >
      <CloudUploadOutlinedIcon sx={{ fontSize: 40, color: 'text.secondary', mb: 1 }} />
      <Typography variant="body1" gutterBottom>
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
