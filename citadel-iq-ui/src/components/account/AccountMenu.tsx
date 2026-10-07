import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Avatar, Box, Chip, Divider, IconButton, ListItemIcon, Menu, MenuItem, Tooltip, Typography } from '@mui/material';
import AdminPanelSettingsOutlinedIcon from '@mui/icons-material/AdminPanelSettingsOutlined';
import LogoutRoundedIcon from '@mui/icons-material/LogoutRounded';
import ExitToAppRoundedIcon from '@mui/icons-material/ExitToAppRounded';
import { ConfirmDialog } from '../common/ConfirmDialog';
import { useToast } from '../common/ToastProvider';
import { organizationApi } from '../../api/organizationApi';
import { ApiError } from '../../api/apiClient';
import { sessionApi } from '../../api/sessionApi';
import { useActiveWorkspace, useSession } from '../../session/useSession';

function initials(name: string) {
  return name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]!.toUpperCase())
    .join('');
}

/** Who you are, which workspace you're in, organization admin, leave, sign out. */
export function AccountMenu() {
  const { user, workspace, role, can } = useActiveWorkspace();
  const { refresh } = useSession();
  const navigate = useNavigate();
  const { showToast } = useToast();
  const [anchor, setAnchor] = useState<HTMLElement | null>(null);
  const [isLeaveOpen, setLeaveOpen] = useState(false);
  const [isLeaving, setIsLeaving] = useState(false);
  const isOrganization = workspace.kind === 'Organization';

  const leave = async () => {
    setIsLeaving(true);
    try {
      await organizationApi.leave();
      setLeaveOpen(false);
      await refresh(); // now "Closed"
    } catch (err) {
      showToast(err instanceof ApiError ? err.message : 'Unable to leave right now.', 'error');
    } finally {
      setIsLeaving(false);
    }
  };

  const signOut = () => {
    // A real form post, so the browser follows the redirect to the identity provider's sign-out page.
    const form = document.createElement('form');
    form.method = 'post';
    form.action = sessionApi.signOutAction;
    document.body.appendChild(form);
    form.submit();
  };

  return (
    <>
      <Tooltip title="Account">
        <IconButton onClick={(e) => setAnchor(e.currentTarget)} aria-label="Account menu" aria-haspopup="menu" aria-expanded={anchor !== null}>
          <Avatar sx={{ width: 32, height: 32, fontSize: 14, fontWeight: 700, bgcolor: 'primary.main' }}>{initials(user.displayName)}</Avatar>
        </IconButton>
      </Tooltip>
      <Menu anchorEl={anchor} open={anchor !== null} onClose={() => setAnchor(null)} slotProps={{ paper: { sx: { minWidth: 260 } } }}>
        <Box sx={{ px: 2, py: 1 }}>
          <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
            {user.displayName}
          </Typography>
          <Typography variant="caption" color="text.secondary" sx={{ display: 'block' }}>
            {user.email}
          </Typography>
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mt: 1 }}>
            <Typography variant="body2" noWrap sx={{ fontWeight: 600, flex: 1, minWidth: 0 }}>
              {workspace.name}
            </Typography>
            <Chip size="small" label={isOrganization ? `Organization · ${role}` : 'Personal'} />
          </Box>
        </Box>
        <Divider />
        {can.administer && (
          <MenuItem
            onClick={() => {
              setAnchor(null);
              navigate('/admin');
            }}
          >
            <ListItemIcon>
              <AdminPanelSettingsOutlinedIcon fontSize="small" />
            </ListItemIcon>
            Organization admin
          </MenuItem>
        )}
        {isOrganization && (
          <MenuItem
            onClick={() => {
              setAnchor(null);
              setLeaveOpen(true);
            }}
          >
            <ListItemIcon>
              <ExitToAppRoundedIcon fontSize="small" />
            </ListItemIcon>
            Leave organization
          </MenuItem>
        )}
        <MenuItem onClick={signOut}>
          <ListItemIcon>
            <LogoutRoundedIcon fontSize="small" />
          </ListItemIcon>
          Sign out
        </MenuItem>
      </Menu>

      <ConfirmDialog
        open={isLeaveOpen}
        title={`Leave ${workspace.name}?`}
        description={
          <Typography variant="body2" color="text.secondary">
            Your account will be closed permanently. Documents you uploaded stay with the organization. To use
            CitadelIQ again you would sign up with a different email.
          </Typography>
        }
        confirmLabel="Leave"
        isConfirming={isLeaving}
        onConfirm={leave}
        onClose={() => setLeaveOpen(false)}
      />
    </>
  );
}
