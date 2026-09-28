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

/** Icon shape still varies by file type; color is left to the caller (via `sx`) so every file
 * type can share one accent instead of a color-per-extension palette. */
export function FileIcon({ fileName, ...iconProps }: FileIconProps) {
  const extension = getExtension(fileName);

  switch (extension) {
    case '.pdf':
      return <PictureAsPdfOutlinedIcon {...iconProps} />;
    case '.docx':
      return <DescriptionOutlinedIcon {...iconProps} />;
    case '.xlsx':
    case '.csv':
      return <GridOnOutlinedIcon {...iconProps} />;
    case '.txt':
      return <ArticleOutlinedIcon {...iconProps} />;
    default:
      return <InsertDriveFileOutlinedIcon {...iconProps} />;
  }
}
