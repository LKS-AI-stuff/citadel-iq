import PictureAsPdfOutlinedIcon from '@mui/icons-material/PictureAsPdfOutlined';
import DescriptionOutlinedIcon from '@mui/icons-material/DescriptionOutlined';
import GridOnOutlinedIcon from '@mui/icons-material/GridOnOutlined';
import ArticleOutlinedIcon from '@mui/icons-material/ArticleOutlined';
import InsertDriveFileOutlinedIcon from '@mui/icons-material/InsertDriveFileOutlined';
import type { SvgIconProps } from '@mui/material';

interface FileIconProps extends SvgIconProps {
  fileName: string;
}

function getExtension(fileName: string): string {
  return fileName.slice(fileName.lastIndexOf('.')).toLowerCase();
}

const ACCENT_COLORS: Record<string, string> = {
  '.pdf': '#dc2626',
  '.docx': '#2563eb',
  '.xlsx': '#16a34a',
  '.csv': '#16a34a',
  '.txt': '#64748b',
};

export function getFileAccentColor(fileName: string): string {
  return ACCENT_COLORS[getExtension(fileName)] ?? '#6366f1';
}

export function FileIcon({ fileName, ...iconProps }: FileIconProps) {
  const extension = getExtension(fileName);
  const color = ACCENT_COLORS[extension];

  switch (extension) {
    case '.pdf':
      return <PictureAsPdfOutlinedIcon {...iconProps} sx={{ color, ...iconProps.sx }} />;
    case '.docx':
      return <DescriptionOutlinedIcon {...iconProps} sx={{ color, ...iconProps.sx }} />;
    case '.xlsx':
    case '.csv':
      return <GridOnOutlinedIcon {...iconProps} sx={{ color, ...iconProps.sx }} />;
    case '.txt':
      return <ArticleOutlinedIcon {...iconProps} sx={{ color, ...iconProps.sx }} />;
    default:
      return <InsertDriveFileOutlinedIcon {...iconProps} />;
  }
}
