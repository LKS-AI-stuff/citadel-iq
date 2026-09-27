import PictureAsPdfOutlinedIcon from '@mui/icons-material/PictureAsPdfOutlined';
import DescriptionOutlinedIcon from '@mui/icons-material/DescriptionOutlined';
import GridOnOutlinedIcon from '@mui/icons-material/GridOnOutlined';
import ArticleOutlinedIcon from '@mui/icons-material/ArticleOutlined';
import InsertDriveFileOutlinedIcon from '@mui/icons-material/InsertDriveFileOutlined';
import type { SvgIconProps } from '@mui/material';

interface FileIconProps extends SvgIconProps {
  fileName: string;
}

export function FileIcon({ fileName, ...iconProps }: FileIconProps) {
  const extension = fileName.slice(fileName.lastIndexOf('.')).toLowerCase();

  switch (extension) {
    case '.pdf':
      return <PictureAsPdfOutlinedIcon {...iconProps} sx={{ color: '#dc2626', ...iconProps.sx }} />;
    case '.docx':
      return <DescriptionOutlinedIcon {...iconProps} sx={{ color: '#2563eb', ...iconProps.sx }} />;
    case '.xlsx':
    case '.csv':
      return <GridOnOutlinedIcon {...iconProps} sx={{ color: '#16a34a', ...iconProps.sx }} />;
    case '.txt':
      return <ArticleOutlinedIcon {...iconProps} sx={{ color: '#64748b', ...iconProps.sx }} />;
    default:
      return <InsertDriveFileOutlinedIcon {...iconProps} />;
  }
}
