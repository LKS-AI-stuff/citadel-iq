import { useState, type MouseEvent, type ReactNode } from 'react';
import { IconButton, ListItemIcon, ListItemText, Menu, MenuItem, Tooltip } from '@mui/material';
import MoreVertIcon from '@mui/icons-material/MoreVert';

export interface CardAction {
  label: string;
  icon: ReactNode;
  onClick: () => void;
  disabled?: boolean;
  disabledReason?: string;
}

interface CardActionsMenuProps {
  actions: CardAction[];
}

export function CardActionsMenu({ actions }: CardActionsMenuProps) {
  const [anchorEl, setAnchorEl] = useState<HTMLElement | null>(null);

  const handleOpen = (e: MouseEvent<HTMLElement>) => {
    e.stopPropagation();
    setAnchorEl(e.currentTarget);
  };

  const handleClose = () => setAnchorEl(null);

  return (
    <>
      <IconButton size="small" onClick={handleOpen} aria-label="More actions">
        <MoreVertIcon fontSize="small" />
      </IconButton>
      <Menu anchorEl={anchorEl} open={Boolean(anchorEl)} onClose={handleClose} onClick={(e) => e.stopPropagation()}>
        {actions.map((action) =>
          action.disabled ? (
            <Tooltip key={action.label} title={action.disabledReason ?? ''} placement="right">
              <span>
                <MenuItem disabled>
                  <ListItemIcon>{action.icon}</ListItemIcon>
                  <ListItemText>{action.label}</ListItemText>
                </MenuItem>
              </span>
            </Tooltip>
          ) : (
            <MenuItem
              key={action.label}
              onClick={() => {
                handleClose();
                action.onClick();
              }}
            >
              <ListItemIcon>{action.icon}</ListItemIcon>
              <ListItemText>{action.label}</ListItemText>
            </MenuItem>
          ),
        )}
      </Menu>
    </>
  );
}
