import { Box, useTheme, type BoxProps } from '@mui/material';
import { glassSurfaceSx } from '../../theme/glass';

interface GlassSurfaceProps extends BoxProps {
  hover?: boolean;
  radius?: number;
}

/** A translucent, backdrop-blurred surface — the building block for the glossy-touch look
 * (header, footer, section panels, cards) so the effect stays consistent everywhere. */
export function GlassSurface({ hover, radius, sx, ...rest }: GlassSurfaceProps) {
  const theme = useTheme();

  return <Box {...rest} sx={{ ...glassSurfaceSx(theme, { hover, radius }), ...sx }} />;
}
